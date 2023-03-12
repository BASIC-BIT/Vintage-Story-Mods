namespace thebasics.ModSystems.ProximityChat.Models;

public record Language(string Name, string Description, string Prefix, string[] Syllables)
{
    public string[] Syllables { get; } = Syllables;
    public string Prefix { get; } = Prefix;
    public string Description { get; } = Description;
    public string Name { get; } = Name;
}