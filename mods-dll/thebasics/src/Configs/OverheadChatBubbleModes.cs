using System;

namespace thebasics.Configs;

public static class OverheadChatBubbleModes
{
    public const string RpText = "RpText";
    public const string Vanilla = "Vanilla";
    public const string Off = "Off";

    public static string Normalize(string mode, bool legacyDisableRpOverheadBubbles = false)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return legacyDisableRpOverheadBubbles ? Vanilla : RpText;
        }

        return mode.Trim() switch
        {
            var value when value.Equals(RpText, StringComparison.OrdinalIgnoreCase) => RpText,
            var value when value.Equals(Vanilla, StringComparison.OrdinalIgnoreCase) => Vanilla,
            var value when value.Equals(Off, StringComparison.OrdinalIgnoreCase) => Off,
            _ => legacyDisableRpOverheadBubbles ? Vanilla : RpText
        };
    }
}
