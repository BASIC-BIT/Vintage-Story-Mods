using NSubstitute;
using Vintagestory.API.Config;

namespace thebasics.Tests.Support;

internal static class LangTestHelper
{
    private static readonly object Sync = new();
    private static bool initialized;

    public static void EnsureEnglish()
    {
        lock (Sync)
        {
            if (!initialized)
            {
                var translationService = Substitute.For<ITranslationService>();
                translationService.Get(Arg.Any<string>(), Arg.Any<object[]>()).Returns(call => call.ArgAt<string>(0));
                Lang.AvailableLanguages["en"] = translationService;
                initialized = true;
            }

            Lang.ChangeLanguage("en");
        }
    }
}
