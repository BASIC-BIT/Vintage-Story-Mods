using System;
using System.Text.RegularExpressions;

namespace thebasics.Utilities;

public static class PatternMatchUtils
{
    /// <summary>
    /// Performs simple wildcard matching supporting '*' and '?' characters.
    /// </summary>
    public static bool WildcardMatches(string input, string pattern, bool ignoreCase = true)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        if (pattern == "*")
        {
            return true;
        }

        var regexPattern = "^" + Regex.Escape(pattern.Trim())
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.IsMatch(input ?? string.Empty, regexPattern, options);
    }
}
