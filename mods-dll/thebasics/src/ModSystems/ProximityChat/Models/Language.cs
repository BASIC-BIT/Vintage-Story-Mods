using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public class Language
{
    public string Name { get; }
    public string Description { get; }
    public string Prefix { get; }
    public string[] Syllables { get; }
    public string Color { get; }
    public bool Default { get; }
    public bool Hidden { get; }
    public bool IsSignLanguage { get; }
    public int SignLanguageRange { get; }
    public bool UseItalics { get; }

    public Language(string name, string description, string prefix, string[] syllables, string color, bool isDefault, bool hidden, bool isSignLanguage = false, int signLanguageRange = 60, bool useItalics = false)
    {
        Name = name;
        Description = description;
        Prefix = prefix;
        Syllables = syllables;
        Color = color;
        Default = isDefault;
        Hidden = hidden;
        IsSignLanguage = isSignLanguage;
        SignLanguageRange = signLanguageRange;
        UseItalics = useItalics;
    }
}