using System.Text;

namespace DoubaoVoiceHotkey;

internal static class Log
{
    private static readonly object Gate = new();

    internal static void Info(string message) => Write("INFO", message, null);
    internal static void Warn(string message) => Write("WARN", message, null);
    internal static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(' ')
                .Append(level)
                .Append(' ')
                .Append(message);
            if (exception is not null)
                line.AppendLine().Append(exception);

            lock (Gate)
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must never terminate the tray process.
        }
    }
}
