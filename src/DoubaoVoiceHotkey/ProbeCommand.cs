namespace DoubaoVoiceHotkey;

internal static class ProbeCommand
{
    internal static string Run(bool live)
    {
        try
        {
            var settings = AppSettings.LoadOrCreate();
            using var controller = new VoiceController(settings);
            var result = controller.SelfCheck(live);
            File.WriteAllText(AppPaths.ProbeFile, result + Environment.NewLine);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("Probe command failed.", ex);
            var result = "FAIL: " + ex;
            File.WriteAllText(AppPaths.ProbeFile, result + Environment.NewLine);
            return result;
        }
    }
}
