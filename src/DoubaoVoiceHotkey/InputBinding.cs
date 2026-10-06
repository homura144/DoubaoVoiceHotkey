namespace DoubaoVoiceHotkey;

internal enum InputBindingKind
{
    Disabled,
    Keyboard,
    MouseXButton
}

internal readonly record struct InputBinding(InputBindingKind Kind, int Code, string Name)
{
    internal bool IsDisabled => Kind == InputBindingKind.Disabled;

    internal static InputBinding Parse(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0 || text.Equals("None", StringComparison.OrdinalIgnoreCase))
            return new(InputBindingKind.Disabled, 0, "None");

        if (text.Equals("XButton1", StringComparison.OrdinalIgnoreCase))
            return new(InputBindingKind.MouseXButton, 1, "XButton1");
        if (text.Equals("XButton2", StringComparison.OrdinalIgnoreCase))
            return new(InputBindingKind.MouseXButton, 2, "XButton2");

        if (text.Length >= 2 && text[0] is 'F' or 'f'
            && int.TryParse(text[1..], out var functionNumber)
            && functionNumber is >= 1 and <= 24)
        {
            return new(InputBindingKind.Keyboard, 0x70 + functionNumber - 1, $"F{functionNumber}");
        }

        var vk = text.ToUpperInvariant() switch
        {
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "RALT" => 0xA5,
            "LALT" => 0xA4,
            "RCTRL" or "RCONTROL" => 0xA3,
            "LCTRL" or "LCONTROL" => 0xA2,
            "RSHIFT" => 0xA1,
            "LSHIFT" => 0xA0,
            "PAUSE" => 0x13,
            "INSERT" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            _ => -1
        };

        if (vk >= 0)
            return new(InputBindingKind.Keyboard, vk, text);

        throw new InvalidDataException(
            $"Unsupported key '{value}'. Supported examples: F8, Escape, RAlt, RCtrl, XButton1, XButton2, None.");
    }

    internal bool Matches(InputSignal signal) =>
        Kind switch
        {
            InputBindingKind.Keyboard => signal.Kind == InputBindingKind.Keyboard && signal.Code == Code,
            InputBindingKind.MouseXButton => signal.Kind == InputBindingKind.MouseXButton && signal.Code == Code,
            _ => false
        };
}

internal readonly record struct InputSignal(InputBindingKind Kind, int Code, bool IsDown, bool IsRepeat);
