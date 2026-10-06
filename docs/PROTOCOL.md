# Protocol notes

DoubaoVoiceHotkey controls the locally installed Doubao IME. It does not call a public cloud API and does not bundle a Doubao DLL.

## Endpoint

Recent independent implementations identify the local named pipe as:

```text
\\.\pipe\ObricIme\oime-server
```

The RPC helper library exports a function named `RpcPipe_SimpleMessage`.

## Voice messages

The following message identifiers have been observed consistently across independent Windows implementations:

| Operation | Decimal | Hex |
| --- | ---: | ---: |
| Start | 1007 | `0x3EF` |
| Stop / finalize | 1008 | `0x3F0` |
| Show voice UI | 1012 | `0x3F4` |
| Cancel | 1013 | `0x3F5` |

There are at least two recent DLL layouts:

1. `doubaoime-rpc-new.dll` near `ImeService.exe`, where recent builds have been observed using a start mode plus a tick-count timestamp.
2. `rpc.dll` in versioned DoubaoIME directories, where independent implementations use the same logical message IDs with different start parameters.

DoubaoVoiceHotkey detects the DLL layout and selects the corresponding call parameters. The Windows x64 bridge sends zero-filled trailing integer argument slots so shorter exports can ignore them while wider observed forms receive deterministic values.

## Evidence and uncertainty

This is an undocumented internal protocol. The message map is treated as a compatibility observation, not as an official contract.

Useful independent public references include:

- `Kong-1024/doubao-voice-switcher` — Windows implementation documenting Start/Stop/Show/Cancel message IDs.
- `littleWhiteDuck/VoiceWinBridge` — independent Windows implementation using the same decimal message IDs.
- `royess/doubao-ime-linux` — experimental bridge documenting the `ObricIme` pipe and `RpcPipe_*` ABI family.
- Microsoft Windows TSF documentation — profile enumeration and activation are implemented through supported Windows interfaces.

No source code from unlicensed repositories is incorporated into this project. Interface names, exported symbols, numeric message identifiers, and observable call behavior are treated as interoperability facts.

## Compatibility rule

A successful DLL load is not enough to claim compatibility. A release should be considered verified for a specific Doubao version only after the live self-check confirms:

1. Doubao profile activation succeeds;
2. Start opens voice capture;
3. Show presents the voice UI;
4. Stop closes the session and commits normally;
5. Cancel safely aborts an active session.

Record verified versions in release notes rather than implying forward compatibility.

## Stop timing

`0x3F0` requests Stop, but the surrounding speech pipeline can still be processing the tail of a fast utterance. The application therefore does not send Stop immediately on the user's second toggle: it keeps a short configurable grace period first, then remains in a `Finalizing` state for a configurable settle period after Stop. These timings are application-side compatibility guards, not documented Doubao protocol requirements.
