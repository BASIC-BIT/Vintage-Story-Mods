using System.Collections.Generic;

namespace PocketDimensions;

internal static class PocketSlug
{
    public static string Normalize(string value)
    {
        value = (value ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new List<char>(value.Length);
        var lastDash = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == ':')
            {
                chars.Add(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                chars.Add('-');
                lastDash = true;
            }
        }

        return new string(chars.ToArray()).Trim('-');
    }
}
