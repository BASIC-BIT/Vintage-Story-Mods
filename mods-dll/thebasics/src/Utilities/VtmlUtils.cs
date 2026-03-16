namespace thebasics.Utilities;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

/// <summary>
/// Utilities for working with VTML (Vintage Story's markup language) and XML/HTML entities.
/// </summary>
public static class VtmlUtils
{
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
