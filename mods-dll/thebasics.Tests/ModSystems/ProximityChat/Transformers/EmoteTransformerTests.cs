using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

public class EmoteTransformerTests
{
    [Fact]
    public void Transform_KeepsNameSeparatorInsideColoredNarrativeSegment()
    {
        var config = new ModConfig
        {
            EnableLanguageSystem = false,
            EmoteColor = "#FF55FF"
        };
        config.InitializeDefaultsIfNeeded();

        var chatSystem = new RPProximityChatSystem { Config = config };
        var transformer = new EmoteTransformer(chatSystem, languageSystem: null!);
        var context = new MessageContext { Message = "waves \"hello\"" };
        context.SetFlag(MessageContext.IS_EMOTE);
        context.SetMetadata(MessageContext.FORMATTED_NAME, "<strong>Alice</strong>");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);

        transformer.Transform(context);

        context.Message.Should().StartWith("<strong>Alice</strong><font color=\"#FF55FF\"> waves");
    }
}
