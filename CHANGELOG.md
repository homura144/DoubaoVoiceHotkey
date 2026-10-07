# Changelog

## 0.1.0-alpha.1

First downloadable Alpha prerelease. Windows 10/11 x64 and Doubao IME version compatibility have not been verified on a physical Windows installation for this release. Successful CI compilation/publishing does not establish runtime compatibility or prove that tail loss is eliminated.

- Fresh clean-room Windows implementation.
- F8 toggle start/stop by default.
- Optional push-to-talk binding.
- Direct `XButton1` / `XButton2` support.
- Esc cancel.
- Windows TSF profile discovery and activation for Doubao IME.
- Local RPC adapters for recent `doubaoime-rpc-new.dll` and versioned `rpc.dll` layouts.
- Passive and live self-checks.
- New project icon and tray UI.
- Include the existing `Finalizing` state so a second toggle cannot immediately start a new voice session while the previous one is still settling.
- Include configurable `StopGraceMs` (default 200 ms) before Stop to preserve the end of fast utterances.
- Include configurable `PostStopSettleMs` (default 400 ms) after Stop before returning to Idle.
- Include live self-check logging with the configured stop/finalization timings.

The tail-loss guard was already present at commit `d99a0ea4aacb4d60f4fe67e8edcf65c278a5e69d`; release preparation changes the publishing workflow and documentation only.
