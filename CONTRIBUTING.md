# Contributing

Contributions are welcome, especially compatibility reports for different Doubao IME versions.

Please keep interoperability work clean-room:

- do not commit proprietary Doubao binaries;
- do not commit decompiled third-party source whose license does not permit redistribution;
- document observable protocol facts separately from implementation code;
- include the exact Doubao version and Windows version in compatibility reports;
- keep new RPC message IDs behind explicit version/probe logic when possible.

Before submitting a pull request:

```powershell
dotnet build .\src\DoubaoVoiceHotkey\DoubaoVoiceHotkey.csproj -c Release
dotnet publish .\src\DoubaoVoiceHotkey\DoubaoVoiceHotkey.csproj -c Release -r win-x64 --self-contained true
```
