using System.Diagnostics;
using System.Drawing;
using System.Reflection;

namespace DoubaoVoiceHotkey;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly Icon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _captureItem;
    private readonly Control _dispatcher;
    private readonly GlobalInputHook _hook;
    private readonly VoiceController _controller;
    private AppSettings _settings;
    private bool _captureEnabled = true;
    private bool _cancelCaptureActive;
    private bool _disposed;

    internal TrayAppContext(AppSettings settings)
    {
        _settings = settings;
        _dispatcher = new Control();
        _dispatcher.CreateControl();

        _controller = new VoiceController(settings);
        _controller.StateChanged += state => Post(() => UpdateStatus(state));
        _controller.Failed += (message, ex) => Post(() => ShowError(message, ex));

        _statusItem = new ToolStripMenuItem("Status: Idle") { Enabled = false };
        _captureItem = new ToolStripMenuItem("Disable key capture", null, (_, _) => ToggleCapture());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_captureItem);
        menu.Items.Add(new ToolStripMenuItem("Open settings", null, (_, _) => OpenFile(AppPaths.SettingsFile)));
        menu.Items.Add(new ToolStripMenuItem("Reload settings", null, (_, _) => ReloadSettings()));
        menu.Items.Add(new ToolStripMenuItem("Open log", null, (_, _) => OpenFile(AppPaths.LogFile)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Run self-check", null, (_, _) => RunSelfCheck(live: false)));
        menu.Items.Add(new ToolStripMenuItem("Run live RPC self-check", null, (_, _) => RunSelfCheck(live: true)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        _trayIcon = LoadTrayIcon();
        _tray = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "DoubaoVoiceHotkey — Idle",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => OpenFile(AppPaths.SettingsFile);

        _hook = new GlobalInputHook { InputReceived = HandleInput };
        UpdateStatus(VoiceState.Idle);
        Log.Info($"Capture active. Toggle={settings.ToggleKey}, Hold={settings.HoldKey}, Cancel={settings.CancelKey}");
    }

    private bool HandleInput(InputSignal signal)
    {
        if (!_captureEnabled)
            return false;

        var settings = _settings;
        if (settings.CancelBinding.Matches(signal))
        {
            if (signal.IsDown)
            {
                if (_controller.State == VoiceState.Listening)
                {
                    _cancelCaptureActive = true;
                    if (!signal.IsRepeat)
                        Post(_controller.Cancel);
                    return true;
                }
                return _cancelCaptureActive;
            }

            if (_cancelCaptureActive)
            {
                _cancelCaptureActive = false;
                return true;
            }
        }

        if (settings.ToggleBinding.Matches(signal))
        {
            if (signal.IsDown && !signal.IsRepeat)
                Post(_controller.Toggle);
            return true;
        }

        if (settings.HoldBinding.Matches(signal))
        {
            if (signal.IsDown && !signal.IsRepeat)
                Post(_controller.StartHold);
            else if (!signal.IsDown)
                Post(_controller.StopHold);
            return true;
        }

        return false;
    }

    private void ToggleCapture()
    {
        _captureEnabled = !_captureEnabled;
        _captureItem.Text = _captureEnabled ? "Disable key capture" : "Enable key capture";
        UpdateStatus(_controller.State);
        Log.Info("Key capture " + (_captureEnabled ? "enabled." : "disabled."));
    }

    private void ReloadSettings()
    {
        try
        {
            var next = AppSettings.LoadOrCreate();
            _settings = next;
            _controller.UpdateSettings(next);
            _tray.ShowBalloonTip(1800, "DoubaoVoiceHotkey",
                $"Settings reloaded. Toggle={next.ToggleKey}, Hold={next.HoldKey}", ToolTipIcon.Info);
            Log.Info("Settings reloaded.");
        }
        catch (Exception ex)
        {
            ShowError("Failed to reload settings.", ex);
        }
    }

    private void RunSelfCheck(bool live)
    {
        if (live)
        {
            var choice = MessageBox.Show(
                "The live self-check will briefly start the microphone through Doubao and then send Stop. Continue?",
                "DoubaoVoiceHotkey",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (choice != DialogResult.OK)
                return;
        }

        var result = _controller.SelfCheck(live);
        File.WriteAllText(AppPaths.ProbeFile, result + Environment.NewLine);
        MessageBox.Show(result, live ? "Live self-check" : "Self-check",
            MessageBoxButtons.OK,
            result.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Information);
    }

    private void UpdateStatus(VoiceState state)
    {
        var prefix = _captureEnabled ? string.Empty : "Capture off — ";
        _statusItem.Text = $"Status: {prefix}{state}";
        var text = $"DoubaoVoiceHotkey — {prefix}{state}";
        _tray.Text = text.Length <= 63 ? text : text[..63];
    }

    private void ShowError(string message, Exception exception)
    {
        Log.Error(message, exception);
        if (_settings.ShowErrorNotifications)
            _tray.ShowBalloonTip(3000, "DoubaoVoiceHotkey", message + "\n" + exception.Message, ToolTipIcon.Error);
    }

    private void Post(Action action)
    {
        if (_disposed || _dispatcher.IsDisposed)
            return;
        try { _dispatcher.BeginInvoke(action); }
        catch (InvalidOperationException) { }
    }

    private static Icon LoadTrayIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DoubaoVoiceHotkey.icon.png")
            ?? throw new InvalidOperationException("Embedded tray icon is missing.");
        using var bitmap = new Bitmap(stream);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tray.Visible = false;
        _hook.Dispose();
        _controller.Dispose();
        _dispatcher.Dispose();
        _tray.Dispose();
        _trayIcon.Dispose();
        Log.Info("Application exiting.");
        base.ExitThreadCore();
    }
}
