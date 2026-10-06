using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DoubaoVoiceHotkey;

internal sealed class DoubaoRpcClient : IDisposable
{
    internal const string PipeName = @"\\.\pipe\ObricIme\oime-server";

    // Undocumented local message identifiers observed independently by multiple implementations.
    private const int VoiceStart = 0x3EF;
    private const int VoiceStop = 0x3F0;
    private const int VoiceShowWave = 0x3F4;
    private const int VoiceCancel = 0x3F5;

    private readonly object _gate = new();
    private IntPtr _library;
    private Rpc8? _rpc;
    private RpcFlavor _flavor;
    private string? _loadedPath;
    private bool _disposed;

    // Windows x64 uses one native calling convention. We intentionally pass four zero-filled
    // trailing integer slots: exports that use the shorter form ignore them, while builds that
    // inspect the extended slots receive deterministic values.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int Rpc8(
        [MarshalAs(UnmanagedType.LPStr)] string pipe,
        int message,
        int wParam,
        int lParam,
        int arg5,
        int arg6,
        int arg7,
        int arg8);

    private enum RpcFlavor
    {
        None,
        NewImeService,
        VersionedRpc
    }

    internal string Describe(int waitMs)
    {
        EnsureReady(waitMs);
        return $"RPC={_loadedPath} | flavor={_flavor} | pipe={(IsPipeReady() ? "ready" : "not-ready")}";
    }

    internal void EnsureReady(int waitMs)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_library != IntPtr.Zero)
                return;

            var deadline = Environment.TickCount64 + waitMs;
            Exception? last = null;
            do
            {
                foreach (var candidate in EnumerateRpcCandidates())
                {
                    try
                    {
                        if (!File.Exists(candidate.Path))
                            continue;
                        Load(candidate);
                        if (!WaitForPipe(deadline))
                        {
                            last = new TimeoutException($"Doubao RPC pipe was not ready after loading {candidate.Path}.");
                            Unload();
                            continue;
                        }
                        Log.Info($"Loaded Doubao RPC: {candidate.Path} ({candidate.Flavor})");
                        return;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Unload();
                    }
                }

                if (Environment.TickCount64 >= deadline)
                    break;
                Thread.Sleep(100);
            } while (true);

            throw new InvalidOperationException(
                "Could not locate or load a compatible Doubao IME RPC library. " +
                "Expected doubaoime-rpc-new.dll beside ImeService.exe or rpc.dll under the DoubaoIME versions directory.",
                last);
        }
    }

    internal void Start(int showWaveDelayMs)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureLoaded();

            int result;
            if (_flavor == RpcFlavor.NewImeService)
            {
                var tick = unchecked((int)NativeMethods.GetTickCount());
                result = Send(VoiceStart, 2, tick);
            }
            else
            {
                result = Send(VoiceStart, 0, 0);
            }
            ThrowOnRpcError("start", result);

            if (showWaveDelayMs > 0)
                Thread.Sleep(showWaveDelayMs);

            result = Send(VoiceShowWave, 1, 0);
            ThrowOnRpcError("show-wave", result);
            Log.Info("Voice started.");
        }
    }

    internal void Stop()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureLoaded();
            var result = Send(VoiceStop, 0, 0);
            ThrowOnRpcError("stop", result);
            Log.Info("Voice stop requested.");
        }
    }

    internal void Cancel()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureLoaded();
            var result = Send(VoiceCancel, 0, 0);
            ThrowOnRpcError("cancel", result);
            Log.Info("Voice cancel requested.");
        }
    }

    internal bool IsPipeReady()
    {
        try
        {
            return NativeMethods.WaitNamedPipe(PipeName, 0);
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitForPipe(long deadline)
    {
        do
        {
            try
            {
                if (NativeMethods.WaitNamedPipe(PipeName, 100))
                    return true;
            }
            catch
            {
                // The service may still be starting. Retry until the configured deadline.
            }

            if (Environment.TickCount64 >= deadline)
                return false;
            Thread.Sleep(50);
        } while (true);
    }

    private int Send(int message, int wParam, int lParam)
    {
        if (_rpc is null)
            throw new InvalidOperationException("Doubao RPC delegate is not initialized.");
        return _rpc(PipeName, message, wParam, lParam, 0, 0, 0, 0);
    }

    private void Load(RpcCandidate candidate)
    {
        _library = NativeLibrary.Load(candidate.Path);
        var export = NativeLibrary.GetExport(_library, "RpcPipe_SimpleMessage");
        _flavor = candidate.Flavor;
        _loadedPath = candidate.Path;
        _rpc = Marshal.GetDelegateForFunctionPointer<Rpc8>(export);
    }

    private static IEnumerable<RpcCandidate> EnumerateRpcCandidates()
    {
        foreach (var process in Process.GetProcessesByName("ImeService"))
        {
            string? directory = null;
            try
            {
                var executable = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executable))
                    directory = Path.GetDirectoryName(executable);
            }
            catch (Exception ex)
            {
                Log.Info($"Skipping ImeService process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }

            if (string.IsNullOrWhiteSpace(directory))
                continue;

            yield return new RpcCandidate(
                Path.Combine(directory, "doubaoime-rpc-new.dll"),
                RpcFlavor.NewImeService);
            yield return new RpcCandidate(
                Path.Combine(directory, "rpc.dll"),
                RpcFlavor.VersionedRpc);
        }

        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DoubaoIME", "versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoubaoIME", "versions")
        };

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var versionDirectory in Directory.EnumerateDirectories(root)
                         .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                yield return new RpcCandidate(
                    Path.Combine(versionDirectory, "rpc.dll"),
                    RpcFlavor.VersionedRpc);
                yield return new RpcCandidate(
                    Path.Combine(versionDirectory, "doubaoime-rpc-new.dll"),
                    RpcFlavor.NewImeService);
            }
        }
    }

    private static void ThrowOnRpcError(string operation, int result)
    {
        if (result != 0)
            throw new InvalidOperationException($"Doubao RPC {operation} failed with 0x{result:X8}.");
    }

    private void EnsureLoaded()
    {
        if (_library == IntPtr.Zero)
            throw new InvalidOperationException("Doubao RPC has not been initialized. Call EnsureReady first.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void Unload()
    {
        if (_library != IntPtr.Zero)
        {
            try { NativeLibrary.Free(_library); }
            catch { }
        }
        _library = IntPtr.Zero;
        _rpc = null;
        _loadedPath = null;
        _flavor = RpcFlavor.None;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            Unload();
        }
    }

    private readonly record struct RpcCandidate(string Path, RpcFlavor Flavor);
}
