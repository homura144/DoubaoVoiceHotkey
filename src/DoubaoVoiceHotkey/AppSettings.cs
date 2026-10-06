using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoubaoVoiceHotkey;

internal sealed class AppSettings
{
    public string ToggleKey { get; set; } = "F8";
    public string HoldKey { get; set; } = "None";
    public string CancelKey { get; set; } = "Escape";
    public bool SwitchToDoubaoBeforeVoice { get; set; } = true;
    public int AfterImeSwitchDelayMs { get; set; } = 350;
    public int ShowWaveDelayMs { get; set; } = 100;
    public int StopGraceMs { get; set; } = 200;
    public int PostStopSettleMs { get; set; } = 400;
    public int RpcWaitMs { get; set; } = 3000;
    public bool ShowErrorNotifications { get; set; } = true;

    [JsonIgnore]
    public InputBinding ToggleBinding => InputBinding.Parse(ToggleKey);

    [JsonIgnore]
    public InputBinding HoldBinding => InputBinding.Parse(HoldKey);

    [JsonIgnore]
    public InputBinding CancelBinding => InputBinding.Parse(CancelKey);

    internal static AppSettings LoadOrCreate()
    {
        AppPaths.EnsureDirectories();
        if (!File.Exists(AppPaths.SettingsFile))
        {
            var created = new AppSettings();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(AppPaths.SettingsFile);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                       ?? throw new InvalidDataException("settings.json is empty or invalid.");
        settings.Validate();
        return settings;
    }

    internal static void Save(AppSettings settings)
    {
        settings.Validate();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.SettingsFile, json + Environment.NewLine);
    }

    private void Validate()
    {
        _ = ToggleBinding;
        _ = HoldBinding;
        _ = CancelBinding;

        if (SameBinding(ToggleBinding, HoldBinding))
            throw new InvalidDataException("ToggleKey and HoldKey cannot be the same binding.");
        if (SameBinding(CancelBinding, ToggleBinding) || SameBinding(CancelBinding, HoldBinding))
            throw new InvalidDataException("CancelKey must be different from ToggleKey and HoldKey.");

        AfterImeSwitchDelayMs = Math.Clamp(AfterImeSwitchDelayMs, 0, 5000);
        ShowWaveDelayMs = Math.Clamp(ShowWaveDelayMs, 0, 2000);
        StopGraceMs = Math.Clamp(StopGraceMs, 0, 1500);
        PostStopSettleMs = Math.Clamp(PostStopSettleMs, 0, 3000);
        RpcWaitMs = Math.Clamp(RpcWaitMs, 0, 15000);
    }

    private static bool SameBinding(InputBinding left, InputBinding right) =>
        !left.IsDisabled && !right.IsDisabled
        && left.Kind == right.Kind
        && left.Code == right.Code;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
