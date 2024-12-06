using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public record Language(string Name, string Description, string Prefix, string[] Syllables, string Color, bool Default = false)
{
    public string[] Syllables { get; } = Syllables;
    public string Prefix { get; } = Prefix;
    public string Description { get; } = Description;
    public string Name { get; } = Name;
    public string Color { get; } = Color;
    public bool Default { get; set; } = Default;
}