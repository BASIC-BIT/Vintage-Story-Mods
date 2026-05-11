namespace thebasics.ModSystems.CharacterSheets.Models;

/// <summary>
/// Where a field appears in the bio dialog. Set per-field on <see cref="CharacterSheetFieldDefinition"/>.
/// Fields without an explicit section default to <see cref="Body"/>.
/// </summary>
public static class CharacterSheetLayoutSections
{
    /// <summary>
    /// Right column next to the headshot, laid out as a 2-column grid (row-major). Best for short
    /// identifying fields: name, nickname, pronouns, species, etc. Long-string fields will look
    /// cramped here — keep those in <see cref="Body"/>.
    /// </summary>
    public const string HeaderSide = "header-side";

    /// <summary>
    /// The default scrollable body, full dialog width. Best for everything that doesn't need to
    /// be visible at a glance.
    /// </summary>
    public const string Body = "body";

    /// <summary>
    /// Returns the canonical section name. Empty/whitespace/unknown values fall back to <see cref="Body"/>.
    /// </summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Body;
        }

        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed switch
        {
            HeaderSide => HeaderSide,
            Body => Body,
            _ => Body
        };
    }
}
