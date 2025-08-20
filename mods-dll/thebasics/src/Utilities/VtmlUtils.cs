namespace thebasics.Utilities;

/// <summary>
/// Utilities for working with VTML (Vintage Story's markup language) and XML/HTML entities
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