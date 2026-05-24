using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace thebasics.Tests.Assets;

public class LanguageAssetTests
{
    private static readonly Regex RawVtmlTagRegex = new("<[^>]+>", RegexOptions.CultureInvariant);

    private static readonly string[] PlainChatKeys =
    [
        "thebasics-help",
        "notes-help-admin",
        "notes-help-personal",
        "chatprefs-help",
        "chatprefs-langcolor-usage",
        "chatprefs-color-usage",
        "notes-error-admin-usage",
        "charsheet-required-warning"
    ];

    [Fact]
    public void PlainChatHelpStringsDoNotContainRawVtmlTags()
    {
        var langDirectory = FindLangDirectory();
        var failures = new List<string>();

        foreach (var file in Directory.EnumerateFiles(langDirectory, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var key in PlainChatKeys)
            {
                if (!document.RootElement.TryGetProperty(key, out var value))
                {
                    continue;
                }

                var text = value.GetString() ?? string.Empty;
                if (RawVtmlTagRegex.IsMatch(text))
                {
                    failures.Add($"{Path.GetFileName(file)}:{key}");
                }
            }
        }

        failures.Should().BeEmpty("plain chat command help must escape placeholders like &lt;player&gt;");
    }

    private static string FindLangDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "mods-dll", "thebasics", "assets", "thebasics", "lang");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find The BASICs language asset directory.");
    }
}
