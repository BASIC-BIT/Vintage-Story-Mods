using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public record Language(string Name, string Description, string Prefix, string[] Syllables, string Color, bool Default = false, bool Hidden = false)
{
    public string Name { get; } = Name;
    public string Description { get; } = Description;
    public string Prefix { get; } = Prefix;
    public string[] Syllables { get; } = Syllables;
    public string Color { get; } = Color;
    public bool Default { get; set; } = Default;
    public bool Hidden { get; set; } = Hidden;
}