using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.Utilities;

internal static class HandbookGuide
{
    public const string OverviewPage = "thebasics-guide";
    public const string CharacterSheetPage = "thebasics-character-sheets";
    public const string NotesPage = "thebasics-notes";

    public static bool Open(ICoreClientAPI capi, string pageCode)
    {
        if (capi?.LinkProtocols != null && capi.LinkProtocols.TryGetValue("handbook", out var handler))
        {
            handler(new LinkTextComponent($"handbook://{pageCode}"));
            return true;
        }

        capi?.ShowChatMessage(Lang.Get("thebasics:guide-open-fallback"));
        return true;
    }
}
