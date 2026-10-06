namespace DoubaoVoiceHotkey;

internal static class AppPaths
{
    internal static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DoubaoVoiceHotkey");

    internal static string LocalDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DoubaoVoiceHotkey");

    internal static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");
    internal static string LogFile => Path.Combine(LocalDirectory, "app.log");
    internal static string ProbeFile => Path.Combine(LocalDirectory, "probe-result.txt");

    internal static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LocalDirectory);
    }
}
