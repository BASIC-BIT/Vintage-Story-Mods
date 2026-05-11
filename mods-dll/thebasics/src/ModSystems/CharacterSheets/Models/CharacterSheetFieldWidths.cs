namespace thebasics.ModSystems.CharacterSheets.Models;

/// <summary>
/// Width hint per field in the scrollable body. Header-side fields use their own grid layout
/// and ignore this; the body layout pairs two consecutive <see cref="Half"/> fields side-by-side
/// on the same row, falling back to full width otherwise.
/// </summary>
public static class CharacterSheetFieldWidths
{
    public const string Full = "full";
    public const string Half = "half";

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Full;
        }

        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed switch
        {
            Half => Half,
            Full => Full,
            _ => Full
        };
    }
}
