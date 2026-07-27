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
    /// Inserts real newlines inside plain-text tokens that render wider than <paramref name="maxWidthPx"/>.
    /// </summary>
    /// <remarks>
    /// Vintage Story's line breaker (<c>TextDrawUtil.getNextWord</c>) only treats space, tab, CR and LF
    /// as break opportunities. <c>TextDrawUtil.Lineize</c> does trim an over-long token to fit, but only
    /// when that token is the first thing on a line (it requires <c>val.Length == 0 &amp;&amp;
    /// startOffsetX == 0</c>). A long unspaced token that follows other text is therefore appended whole
    /// and clipped by the Cairo clip rectangle. Splitting such tokens before the text reaches the engine
    /// means the engine only ever sees tokens that fit, so its ordinary word wrapping handles them.
    ///
    /// Zero-width space, soft hyphen and word joiner do not work here: they are not break opportunities
    /// for <c>getNextWord</c>, so they only add an invisible glyph.
    ///
    /// Tags are copied verbatim and act as token boundaries, so nothing is ever inserted inside markup.
    /// </remarks>
    /// <param name="vtml">VTML source.</param>
    /// <param name="measureWidthPx">Measures the rendered width of a plain-text run, in pixels.</param>
    /// <param name="maxWidthPx">Widest a single token may render.</param>
    public static string BreakLongTokens(string vtml, System.Func<string, double> measureWidthPx, double maxWidthPx)
    {
        if (string.IsNullOrEmpty(vtml) || measureWidthPx == null || maxWidthPx <= 0)
        {
            return vtml;
        }

        var output = new StringBuilder(vtml.Length + 16);
        var token = new StringBuilder();
        var index = 0;

        while (index < vtml.Length)
        {
            var c = vtml[index];
            if (c != '<' && !IsLineBreakOpportunity(c))
            {
                token.Append(c);
                index++;
                continue;
            }

            AppendBrokenToken(output, token, measureWidthPx, maxWidthPx);
            index += CopyTokenSeparator(output, vtml, index);
        }

        AppendBrokenToken(output, token, measureWidthPx, maxWidthPx);
        return output.ToString();
    }

    /// <summary>
    /// The only characters <c>TextDrawUtil.getNextWord</c> treats as break opportunities.
    /// </summary>
    private static bool IsLineBreakOpportunity(char c)
    {
        return c is ' ' or '\t' or '\r' or '\n';
    }

    /// <summary>
    /// Copies a whole tag, or a single break character, verbatim. Returns the number of characters consumed.
    /// </summary>
    private static int CopyTokenSeparator(StringBuilder output, string vtml, int index)
    {
        if (vtml[index] != '<')
        {
            output.Append(vtml[index]);
            return 1;
        }

        // Unterminated tag: copy the remainder untouched rather than guess where it ends.
        var tagEnd = vtml.IndexOf('>', index);
        var length = tagEnd < 0 ? vtml.Length - index : tagEnd - index + 1;
        output.Append(vtml, index, length);
        return length;
    }

    private static void AppendBrokenToken(StringBuilder output, StringBuilder token, System.Func<string, double> measureWidthPx, double maxWidthPx)
    {
        if (token.Length == 0)
        {
            return;
        }

        var text = token.ToString();
        token.Clear();

        if (measureWidthPx(text) <= maxWidthPx)
        {
            output.Append(text);
            return;
        }

        var chunk = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var unit = text.Substring(index, GetAtomicUnitLength(text, index));
            if (chunk.Length > 0 && measureWidthPx(chunk.ToString() + unit) > maxWidthPx)
            {
                output.Append(chunk).Append('\n');
                chunk.Clear();
            }

            chunk.Append(unit);
            index += unit.Length;
        }

        output.Append(chunk);
    }

    /// <summary>
    /// Length of the smallest unit at <paramref name="index"/> that must not be split.
    /// </summary>
    private static int GetAtomicUnitLength(string text, int index)
    {
        var c = text[index];

        // VtmlParser decodes &lt; &gt; &nbsp; after we run, so an entity must stay intact.
        if (c == '&')
        {
            var limit = Math.Min(text.Length, index + 10);
            for (var i = index + 1; i < limit; i++)
            {
                if (text[i] == ';')
                {
                    return i - index + 1;
                }

                if (!char.IsLetterOrDigit(text[i]) && text[i] != '#')
                {
                    break;
                }
            }
        }

        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return 2;
        }

        return 1;
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
