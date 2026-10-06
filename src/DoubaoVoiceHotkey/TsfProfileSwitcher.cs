using System.Runtime.InteropServices;

namespace DoubaoVoiceHotkey;

internal sealed class TsfProfileSwitcher
{
    private const uint TfProfileTypeInputProcessor = 0x0001;
    private const uint TfIppmfForSession = 0x20000000;
    private const uint TfIppmfEnableProfile = 0x00000001;
    private const uint TfIppmfDontCareCurrentInputLanguage = 0x00000004;

    private static readonly Guid ClsidTfInputProcessorProfiles =
        new("33C53A50-F456-4884-B049-85FD643ECFED");

    private readonly object _gate = new();
    private TfInputProcessorProfile? _cachedDoubao;

    internal string DescribeDoubaoProfile()
    {
        var profile = FindDoubaoProfile();
        return $"{profile.Description} | lang=0x{profile.Profile.langid:X4} | clsid={profile.Profile.clsid} | profile={profile.Profile.guidProfile}";
    }

    internal void ActivateDoubao()
    {
        var target = FindDoubaoProfile();
        object? comObject = null;
        try
        {
            comObject = CreateProfilesComObject();
            var manager = (ITfInputProcessorProfileMgr)comObject;
            var clsid = target.Profile.clsid;
            var profileGuid = target.Profile.guidProfile;
            var flags = TfIppmfForSession | TfIppmfEnableProfile | TfIppmfDontCareCurrentInputLanguage;
            var hr = manager.ActivateProfile(
                TfProfileTypeInputProcessor,
                target.Profile.langid,
                ref clsid,
                ref profileGuid,
                IntPtr.Zero,
                flags);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
            if (hr != 0)
                throw new InvalidOperationException($"TSF ActivateProfile returned 0x{hr:X8}.");

            Log.Info($"Activated Doubao TSF profile: {target.Description}");
        }
        finally
        {
            ReleaseCom(comObject);
        }
    }

    private NamedProfile FindDoubaoProfile()
    {
        lock (_gate)
        {
            if (_cachedDoubao is { } cached)
                return new NamedProfile(cached, "豆包输入法 (cached)");

            object? comObject = null;
            IEnumTfInputProcessorProfiles? enumerator = null;
            try
            {
                comObject = CreateProfilesComObject();
                var manager = (ITfInputProcessorProfileMgr)comObject;
                var descriptions = (ITfInputProcessorProfiles)comObject;

                var hr = manager.EnumProfiles(0, out var createdEnumerator);
                enumerator = createdEnumerator;
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                while (true)
                {
                    hr = enumerator.Next(1, out var profile, out var fetched);
                    if (hr < 0)
                        Marshal.ThrowExceptionForHR(hr);
                    if (fetched == 0)
                        break;
                    if (profile.dwProfileType != TfProfileTypeInputProcessor)
                        continue;

                    var clsid = profile.clsid;
                    var profileGuid = profile.guidProfile;
                    string description;
                    try
                    {
                        var descHr = descriptions.GetLanguageProfileDescription(
                            ref clsid,
                            profile.langid,
                            ref profileGuid,
                            out var resolvedDescription);
                        description = resolvedDescription;
                        if (descHr < 0 || string.IsNullOrWhiteSpace(description))
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    if (!description.Contains("豆包", StringComparison.OrdinalIgnoreCase)
                        && !description.Contains("Doubao", StringComparison.OrdinalIgnoreCase))
                        continue;

                    _cachedDoubao = profile;
                    Log.Info($"Found Doubao TSF profile: {description}");
                    return new NamedProfile(profile, description);
                }

                throw new InvalidOperationException(
                    "Could not find a TSF language profile whose description contains '豆包' or 'Doubao'. " +
                    "Confirm Doubao IME is installed and enabled in Windows language settings.");
            }
            finally
            {
                ReleaseCom(enumerator);
                ReleaseCom(comObject);
            }
        }
    }

    private static object CreateProfilesComObject()
    {
        var type = Type.GetTypeFromCLSID(ClsidTfInputProcessorProfiles, throwOnError: true)
                   ?? throw new InvalidOperationException("TSF profile manager COM class is unavailable.");
        return Activator.CreateInstance(type)
               ?? throw new InvalidOperationException("Failed to create TSF profile manager COM object.");
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); }
            catch { }
        }
    }

    private readonly record struct NamedProfile(TfInputProcessorProfile Profile, string Description);

    [StructLayout(LayoutKind.Sequential)]
    private struct TfInputProcessorProfile
    {
        public uint dwProfileType;
        public ushort langid;
        public Guid clsid;
        public Guid guidProfile;
        public Guid catid;
        public IntPtr hklSubstitute;
        public uint dwCaps;
        public IntPtr hkl;
        public uint dwFlags;
    }

    [ComImport]
    [Guid("71C6E74D-0F28-11D8-A82A-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumTfInputProcessorProfiles
    {
        [PreserveSig]
        int Clone(out IEnumTfInputProcessorProfiles enumerator);

        [PreserveSig]
        int Next(uint count, out TfInputProcessorProfile profile, out uint fetched);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Skip(uint count);
    }

    [ComImport]
    [Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfInputProcessorProfileMgr
    {
        [PreserveSig]
        int ActivateProfile(uint profileType, ushort langid, ref Guid clsid, ref Guid profileGuid, IntPtr hkl, uint flags);

        [PreserveSig]
        int DeactivateProfile(uint profileType, ushort langid, ref Guid clsid, ref Guid profileGuid, IntPtr hkl, uint flags);

        [PreserveSig]
        int GetProfile(uint profileType, ushort langid, ref Guid clsid, ref Guid profileGuid, IntPtr hkl,
            out TfInputProcessorProfile profile);

        [PreserveSig]
        int EnumProfiles(ushort langid, out IEnumTfInputProcessorProfiles enumerator);

        [PreserveSig]
        int ReleaseInputProcessor(ref Guid clsid, uint flags);

        [PreserveSig]
        int RegisterProfile(ref Guid clsid, ushort langid, ref Guid profileGuid,
            [MarshalAs(UnmanagedType.LPWStr)] string description, uint descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string iconFile, uint iconFileLength,
            uint iconIndex, IntPtr substituteHkl, uint preferredLayout, int enabledByDefault, uint flags);

        [PreserveSig]
        int UnregisterProfile(ref Guid clsid, ushort langid, ref Guid profileGuid, uint flags);

        [PreserveSig]
        int GetActiveProfile(ref Guid category, out TfInputProcessorProfile profile);
    }

    [ComImport]
    [Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfInputProcessorProfiles
    {
        [PreserveSig] int Register(ref Guid clsid);
        [PreserveSig] int Unregister(ref Guid clsid);

        [PreserveSig]
        int AddLanguageProfile(ref Guid clsid, ushort langid, ref Guid profileGuid,
            [MarshalAs(UnmanagedType.LPWStr)] string description, uint descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string iconFile, uint iconFileLength, uint iconIndex);

        [PreserveSig] int RemoveLanguageProfile(ref Guid clsid, ushort langid, ref Guid profileGuid);
        [PreserveSig] int EnumInputProcessorInfo(out IntPtr enumerator);

        [PreserveSig]
        int GetDefaultLanguageProfile(ushort langid, ref Guid category, out Guid clsid, out Guid profileGuid);

        [PreserveSig]
        int SetDefaultLanguageProfile(ushort langid, ref Guid clsid, ref Guid profileGuid);

        [PreserveSig]
        int ActivateLanguageProfile(ref Guid clsid, ushort langid, ref Guid profileGuid);

        [PreserveSig]
        int GetActiveLanguageProfile(ref Guid clsid, out ushort langid, out Guid profileGuid);

        [PreserveSig]
        int GetLanguageProfileDescription(ref Guid clsid, ushort langid, ref Guid profileGuid,
            [MarshalAs(UnmanagedType.BStr)] out string description);

        [PreserveSig] int GetCurrentLanguage(out ushort langid);
        [PreserveSig] int ChangeCurrentLanguage(ushort langid);
        [PreserveSig] int GetLanguageList(out IntPtr langIds, out uint count);
        [PreserveSig] int EnumLanguageProfiles(ushort langid, out IntPtr enumerator);
        [PreserveSig] int EnableLanguageProfile(ref Guid clsid, ushort langid, ref Guid profileGuid, int enable);
        [PreserveSig] int IsEnabledLanguageProfile(ref Guid clsid, ushort langid, ref Guid profileGuid, out int enabled);
        [PreserveSig] int EnableLanguageProfileByDefault(ref Guid clsid, ushort langid, ref Guid profileGuid, int enable);
        [PreserveSig] int SubstituteKeyboardLayout(ref Guid clsid, ushort langid, ref Guid profileGuid, IntPtr hkl);
    }
}
