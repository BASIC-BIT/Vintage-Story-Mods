using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace thebasics.Utilities;

public static class PatternMatchUtils
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

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

        var options = RegexOptions.Compiled | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        var cacheKey = $"{(ignoreCase ? 'i' : 's')}:{regexPattern}";
        var regex = RegexCache.GetOrAdd(cacheKey, _ => new Regex(regexPattern, options));
        return regex.IsMatch(input ?? string.Empty);
    }
}
