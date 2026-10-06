# Changelog

## Unreleased

- Add a `Finalizing` state so a second toggle cannot immediately start a new voice session while the previous one is still settling.
- Add configurable `StopGraceMs` (default 200 ms) before Stop to preserve the end of fast utterances.
- Add configurable `PostStopSettleMs` (default 400 ms) after Stop before returning to Idle.
- Extend live self-check logging with the configured stop/finalization timings.

## 0.1.0-alpha.1

- Fresh clean-room Windows implementation.
- F8 toggle start/stop by default.
- Optional push-to-talk binding.
- Direct `XButton1` / `XButton2` support.
- Esc cancel.
- Windows TSF profile discovery and activation for Doubao IME.
- Local RPC adapters for recent `doubaoime-rpc-new.dll` and versioned `rpc.dll` layouts.
- Passive and live self-checks.
- New project icon and tray UI.
