using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace thebasics.ModSystems.HomeSpawn;

[ProtoContract]
public class HomeSpawnHomeRegistry
{
    public const string DefaultHomeName = "default";
    public const int MaxHomeNameLength = 32;

    [ProtoMember(1)]
    public List<HomeSpawnHomeEntry> Homes { get; set; } = new();

    public static string NormalizeName(string name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? DefaultHomeName : name.Trim();
        return normalized.Equals(DefaultHomeName, StringComparison.OrdinalIgnoreCase)
            ? DefaultHomeName
            : normalized.ToLowerInvariant();
    }

    public static bool IsValidName(string name, out string errorCode)
    {
        errorCode = null;
        var normalized = NormalizeName(name);
        if (normalized.Length > MaxHomeNameLength)
        {
            errorCode = "too-long";
            return false;
        }

        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
        {
            errorCode = "invalid-characters";
            return false;
        }

        return true;
    }

    public bool TryGetHome(string name, out HomeSpawnLocation location)
    {
        var normalized = NormalizeName(name);
        location = Homes.FirstOrDefault(home => NamesEqual(home.Name, normalized))?.Location;
        return location != null;
    }

    public bool TrySetHome(string name, HomeSpawnLocation location, int maxHomes, out string normalizedName)
    {
        normalizedName = NormalizeName(name);
        var homeKey = normalizedName;
        var existing = Homes.FirstOrDefault(home => NamesEqual(home.Name, homeKey));
        if (existing != null)
        {
            existing.Name = normalizedName;
            existing.Location = location;
            return true;
        }

        if (Homes.Count >= Math.Max(1, maxHomes))
        {
            return false;
        }

        Homes.Add(new HomeSpawnHomeEntry { Name = normalizedName, Location = location });
        SortHomes();
        return true;
    }

    public bool RemoveHome(string name, out string normalizedName)
    {
        normalizedName = NormalizeName(name);
        var homeKey = normalizedName;
        var removed = Homes.RemoveAll(home => NamesEqual(home.Name, homeKey));
        return removed > 0;
    }

    public IReadOnlyList<HomeSpawnHomeEntry> ListHomes()
    {
        SortHomes();
        return Homes;
    }

    private void SortHomes()
    {
        Homes = Homes
            .Where(home => home?.Location != null && !string.IsNullOrWhiteSpace(home.Name))
            .OrderBy(home => home.Name.Equals(DefaultHomeName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(home => home.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool NamesEqual(string left, string right)
    {
        return string.Equals(NormalizeName(left), NormalizeName(right), StringComparison.OrdinalIgnoreCase);
    }
}
