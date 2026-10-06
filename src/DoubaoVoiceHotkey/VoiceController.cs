namespace DoubaoVoiceHotkey;

internal enum VoiceState
{
    Idle,
    Starting,
    Listening,
    Finalizing
}

internal sealed class VoiceController : IDisposable
{
    private readonly object _gate = new();
    private readonly TsfProfileSwitcher _switcher = new();
    private readonly DoubaoRpcClient _rpc = new();
    private AppSettings _settings;
    private VoiceState _state = VoiceState.Idle;
    private bool _disposed;

    internal event Action<VoiceState>? StateChanged;
    internal event Action<string, Exception>? Failed;

    internal VoiceController(AppSettings settings)
    {
        _settings = settings;
    }

    internal VoiceState State
    {
        get { lock (_gate) return _state; }
    }

    internal void UpdateSettings(AppSettings settings)
    {
        lock (_gate)
            _settings = settings;
    }

    internal void Toggle()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            if (_state == VoiceState.Idle)
                StartCore();
            else if (_state == VoiceState.Listening)
                StopCore();
        }
    }

    internal void StartHold()
    {
        lock (_gate)
        {
            if (_disposed || _state != VoiceState.Idle)
                return;
            StartCore();
        }
    }

    internal void StopHold()
    {
        lock (_gate)
        {
            if (_disposed || _state != VoiceState.Listening)
                return;
            StopCore();
        }
    }

    internal void Cancel()
    {
        lock (_gate)
        {
            if (_disposed || _state is VoiceState.Idle or VoiceState.Starting)
                return;
            try
            {
                SetState(VoiceState.Finalizing);
                _rpc.Cancel();
                SetState(VoiceState.Idle);
            }
            catch (Exception ex)
            {
                Log.Error("Cancel failed.", ex);
                SetState(VoiceState.Idle);
                Failed?.Invoke("Failed to cancel Doubao voice input.", ex);
            }
        }
    }

    internal string SelfCheck(bool live)
    {
        lock (_gate)
        {
            var lines = new List<string>();
            try
            {
                lines.Add("TSF: " + _switcher.DescribeDoubaoProfile());
                lines.Add("RPC: " + _rpc.Describe(_settings.RpcWaitMs));
                if (live)
                {
                    if (_settings.SwitchToDoubaoBeforeVoice)
                    {
                        _switcher.ActivateDoubao();
                        if (_settings.AfterImeSwitchDelayMs > 0)
                            Thread.Sleep(_settings.AfterImeSwitchDelayMs);
                    }
                    _rpc.Start(_settings.ShowWaveDelayMs);
                    Thread.Sleep(800);
                    if (_settings.StopGraceMs > 0)
                        Thread.Sleep(_settings.StopGraceMs);
                    _rpc.Stop();
                    if (_settings.PostStopSettleMs > 0)
                        Thread.Sleep(_settings.PostStopSettleMs);
                    lines.Add($"LIVE: start/show/stop completed without an RPC error (grace={_settings.StopGraceMs}ms, settle={_settings.PostStopSettleMs}ms).");
                }
                lines.Add("PASS");
            }
            catch (Exception ex)
            {
                Log.Error("Self-check failed.", ex);
                lines.Add("FAIL: " + ex.Message);
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    private void StartCore()
    {
        try
        {
            SetState(VoiceState.Starting);
            var settings = _settings;

            if (settings.SwitchToDoubaoBeforeVoice)
            {
                _switcher.ActivateDoubao();
                if (settings.AfterImeSwitchDelayMs > 0)
                    Thread.Sleep(settings.AfterImeSwitchDelayMs);
            }

            _rpc.EnsureReady(settings.RpcWaitMs);
            _rpc.Start(settings.ShowWaveDelayMs);
            SetState(VoiceState.Listening);
        }
        catch (Exception ex)
        {
            Log.Error("Start voice failed.", ex);
            SetState(VoiceState.Idle);
            Failed?.Invoke("Failed to start Doubao voice input.", ex);
        }
    }

    private void StopCore()
    {
        var settings = _settings;
        SetState(VoiceState.Finalizing);
        Log.Info($"Finalizing voice: stop grace={settings.StopGraceMs}ms, post-stop settle={settings.PostStopSettleMs}ms.");

        _ = Task.Run(() => FinalizeCore(settings));
    }

    private void FinalizeCore(AppSettings settings)
    {
        try
        {
            if (settings.StopGraceMs > 0)
                Thread.Sleep(settings.StopGraceMs);

            lock (_gate)
            {
                if (_disposed || _state != VoiceState.Finalizing)
                    return;
            }

            _rpc.Stop();
            Log.Info("Stop RPC sent after grace period.");

            if (settings.PostStopSettleMs > 0)
                Thread.Sleep(settings.PostStopSettleMs);

            lock (_gate)
            {
                if (_disposed || _state != VoiceState.Finalizing)
                    return;
                SetState(VoiceState.Idle);
            }

            Log.Info("Voice finalization settle period completed.");
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (!_disposed && _state == VoiceState.Finalizing)
                    SetState(VoiceState.Idle);
            }
            Log.Error("Stop voice failed.", ex);
            Failed?.Invoke("Failed to stop Doubao voice input.", ex);
        }
    }

    private void SetState(VoiceState state)
    {
        _state = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_state is VoiceState.Listening or VoiceState.Finalizing)
            {
                try { _rpc.Cancel(); }
                catch { }
            }
            _rpc.Dispose();
            _state = VoiceState.Idle;
        }
    }
}
