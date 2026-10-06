namespace DoubaoVoiceHotkey;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var singleton = new Mutex(true, @"Local\DoubaoVoiceHotkey", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("DoubaoVoiceHotkey is already running.", "DoubaoVoiceHotkey",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppPaths.EnsureDirectories();
        Log.Info("Application starting.");

        if (args.Any(x => string.Equals(x, "--probe", StringComparison.OrdinalIgnoreCase)))
        {
            var result = ProbeCommand.Run(live: false);
            MessageBox.Show(result, "DoubaoVoiceHotkey self-check",
                MessageBoxButtons.OK,
                result.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
            return;
        }

        if (args.Any(x => string.Equals(x, "--live-probe", StringComparison.OrdinalIgnoreCase)))
        {
            var result = ProbeCommand.Run(live: true);
            MessageBox.Show(result, "DoubaoVoiceHotkey live self-check",
                MessageBoxButtons.OK,
                result.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
            return;
        }

        try
        {
            var settings = AppSettings.LoadOrCreate();
            Application.Run(new TrayAppContext(settings));
        }
        catch (Exception ex)
        {
            Log.Error("Fatal startup error.", ex);
            MessageBox.Show(ex.ToString(), "DoubaoVoiceHotkey startup error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
