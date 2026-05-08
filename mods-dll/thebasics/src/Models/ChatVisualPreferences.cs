using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ChatVisualPreferences
{
    [ProtoMember(1)]
    public int SchemaVersion { get; set; } = 1;

    [ProtoMember(2)]
    public bool LanguageColorsEnabled { get; set; } = true;

    [ProtoMember(3)]
    public bool ShowLanguageLabels { get; set; }

    [ProtoMember(4)]
    public string ColorPreset { get; set; } = ChatVisualPreferencePresets.Default;

    [ProtoMember(5)]
    public List<ColorOverrideEntry> LanguageColorOverrides { get; set; } = new();

    [ProtoMember(6)]
    public bool NicknameColorsEnabled { get; set; } = true;

    [ProtoMember(7)]
    public string OocColorOverride { get; set; }

    [ProtoMember(8)]
    public string GlobalOocColorOverride { get; set; }

    [ProtoMember(9)]
    public string EmoteColorOverride { get; set; }
}

public static class ChatVisualPreferencePresets
{
    public const string Default = "default";
    public const string HighContrast = "highcontrast";
    public const string ColorUniversal = "coloruniversal";
    public const string Protanopia = "protanopia";
    public const string Deuteranopia = "deuteranopia";
    public const string Tritanopia = "tritanopia";
    public const string Monochrome = "monochrome";
}
