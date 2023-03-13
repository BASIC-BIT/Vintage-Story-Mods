using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public record Language(string Name, string Description, string Prefix, string[] Syllables, string Color)
{
    public string[] Syllables { get; } = Syllables;
    public string Prefix { get; } = Prefix;
    public string Description { get; } = Description;
    public string Name { get; } = Name;
    public string Color { get; } = Color;

    private sealed class LanguageEqualityComparer : IEqualityComparer<Language>
    {
        public bool Equals(Language x, Language y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return Equals(x.Syllables, y.Syllables) && x.Prefix == y.Prefix && x.Description == y.Description && x.Name == y.Name;
        }

        public int GetHashCode(Language obj)
        {
            unchecked
            {
                var hashCode = (obj.Syllables != null ? obj.Syllables.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Prefix != null ? obj.Prefix.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Description != null ? obj.Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Name != null ? obj.Name.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public static IEqualityComparer<Language> LanguageComparer { get; } = new LanguageEqualityComparer();

    public virtual bool Equals(Language other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Syllables, other.Syllables) && Prefix == other.Prefix && Description == other.Description && Name == other.Name;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Syllables != null ? Syllables.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Prefix != null ? Prefix.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            return hashCode;
        }
    }
}