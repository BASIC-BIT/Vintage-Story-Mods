namespace thebasics.Utilities;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

/// <summary>
/// Utilities for working with VTML (Vintage Story's markup language) and XML/HTML entities.
/// </summary>
public static class VtmlUtils
{
    private static readonly Regex RawTagRegex = new(@"<(/?)([A-Za-z][A-Za-z0-9:_-]*)([^<>]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EscapedTagRegex = new(@"&lt;(/?)([A-Za-z][A-Za-z0-9:_-]*)((?:(?!&gt;).)*)&gt;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> RenderableTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "br",
        "clear",
        "code",
        "font",
        "hk",
        "hotkey",
        "i",
        "icon",
        "itemstack",
        "strong",
    };

    /// <summary>
    /// Escapes XML/HTML special characters to prevent VTML injection
    /// Note: Vintage Story only escapes < and > in practice, not & " '
    /// </summary>
    public static string EscapeVtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Only escape the critical characters that VS escapes
        // VS doesn't escape &, ", or ' in chat messages
        return input
            .Replace("<", "&lt;")   // Less-than
            .Replace(">", "&gt;");   // Greater-than
    }

    /// <summary>
    /// Removes VTML tags and returns plain text.
    ///
    /// Use this when a rendering surface does not support VTML (e.g. vanilla overhead speech bubbles).
    /// </summary>
    public static string StripVtmlTags(string input, ILogger errorLogger = null)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Prefer the game's own parser to avoid regex edge cases.
        // If we don't have a logger, fall back to a conservative regex strip.
        if (errorLogger == null)
        {
            return Regex.Replace(input, "<[^>]+>", string.Empty);
        }

        try
        {
            var tokens = VtmlParser.Tokenize(errorLogger, input);
            var sb = new StringBuilder(input.Length);
            AppendPlainText(tokens, sb);
            return sb.ToString();
        }
        catch
        {
            return Regex.Replace(input, "<[^>]+>", string.Empty);
        }
    }

    private static void AppendPlainText(IEnumerable<VtmlToken> tokens, StringBuilder sb)
    {
        foreach (var token in tokens)
        {
            if (token is VtmlTextToken text)
            {
                sb.Append(text.Text);
                continue;
            }

            if (token is VtmlTagToken tag)
            {
                if (tag.Name == "br")
                {
                    sb.Append('\n');
                    continue;
                }

                if (tag.ChildElements != null && tag.ChildElements.Count > 0)
                {
                    AppendPlainText(tag.ChildElements, sb);
                }
            }
        }
    }

    /// <summary>
    /// Unescapes XML/HTML entities back to their original characters
    /// </summary>
    public static string UnescapeVtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return input
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&nbsp;", " ");  // VS also handles non-breaking space
    }

    /// <summary>
    /// Removes tag-shaped user input before The BASICs adds its own trusted VTML formatting.
    /// </summary>
    public static string StripUserVtmlTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var stripped = EscapedTagRegex.Replace(input, string.Empty);
        return RawTagRegex.Replace(stripped, string.Empty);
    }

    /// <summary>
    /// Unescapes only the known VTML tags that The BASICs emits for bubble rendering.
    /// </summary>
    public static string UnescapeRenderableVtmlTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return EscapedTagRegex.Replace(input, match =>
        {
            var tagName = match.Groups[2].Value;
            if (!RenderableTags.Contains(tagName))
            {
                return match.Value;
            }

            return BuildRawTag(match.Groups[1].Value, tagName, match.Groups[3].Value);
        });
    }

    /// <summary>
    /// Keeps only VTML tags supported by Vintage Story's richtext renderer.
    /// </summary>
    public static string NormalizeVtmlForRendering(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return RawTagRegex.Replace(input, match =>
        {
            var tagName = match.Groups[2].Value;
            if (!RenderableTags.Contains(tagName))
            {
                return string.Empty;
            }

            return BuildRawTag(match.Groups[1].Value, tagName, match.Groups[3].Value);
        });
    }

    private static string BuildRawTag(string slash, string tagName, string attributes)
    {
        return $"<{slash}{tagName.ToLowerInvariant()}{attributes}>";
    }

    /// <summary>
    /// Checks if a string contains the critical VTML characters that need escaping
    /// </summary>
    public static bool ContainsVtmlSpecialChars(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        return input.Contains('<') || input.Contains('>');
    }

    /// <summary>
    /// Checks if a string contains the critical VTML characters that break parsing
    /// Same as ContainsVtmlSpecialChars since VS only escapes < and >
    /// </summary>
    public static bool ContainsVtmlCriticalChars(string input)
    {
        return ContainsVtmlSpecialChars(input);
    }
}
