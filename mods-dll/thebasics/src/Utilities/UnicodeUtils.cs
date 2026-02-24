using System.Globalization;

namespace thebasics.Utilities;

public static class UnicodeUtils
{
    public static bool IsDecoratorChar(char c)
    {
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        return cat == UnicodeCategory.NonSpacingMark
               || cat == UnicodeCategory.SpacingCombiningMark
               || cat == UnicodeCategory.EnclosingMark
               || cat == UnicodeCategory.Format;
    }
}
