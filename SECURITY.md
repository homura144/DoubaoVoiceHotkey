# Security

DoubaoVoiceHotkey is a local Windows utility that loads an RPC DLL from the locally installed Doubao IME and calls an undocumented local interface.

## Trust boundary

- The project does not download or redistribute Doubao binaries.
- RPC DLLs are loaded only from paths discovered from the local `ImeService.exe` process or known DoubaoIME installation directories.
- The app does not intentionally transmit dictated text or audio itself. Doubao IME remains responsible for its own processing and network behavior.
- Global keyboard/mouse hooks are used only to recognize configured trigger keys.

## Reporting

Please report security-sensitive issues privately to the repository owner rather than opening a public issue with exploit details.

## Internal interface risk

The Doubao RPC protocol is undocumented and can change. A changed DLL ABI may cause crashes. Keep the app and Doubao IME updated together and run the self-check after Doubao upgrades.
