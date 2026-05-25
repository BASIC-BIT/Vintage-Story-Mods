using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class ChatPreferencesCommandHandlerTests
{
    [Theory]
    [InlineData("labels")]
    [InlineData("languagelabels")]
    public void Handle_TogglesBooleanPreferenceByAlias(string setting)
    {
        var player = CreatePlayer();
        var handler = CreateHandler();

        var result = handler.Handle(player, setting, "on", null);

        result.Status.Should().Be(EnumCommandStatus.Success);
        player.GetChatVisualPreferences().ShowLanguageLabels.Should().BeTrue();
    }

    [Fact]
    public void Handle_RejectsInvalidOnOffValueWithoutChangingPreference()
    {
        var player = CreatePlayer();
        var handler = CreateHandler();

        var result = handler.Handle(player, "labels", "maybe", null);

        result.Status.Should().Be(EnumCommandStatus.Error);
        player.GetChatVisualPreferences().ShowLanguageLabels.Should().BeFalse();
    }

    [Fact]
    public void Handle_SetsAndClearsLanguageColorOverride()
    {
        var player = CreatePlayer();
        var handler = CreateHandler();

        handler.Handle(player, "langcolor", "tr", "#123456").Status.Should().Be(EnumCommandStatus.Success);
        player.GetChatVisualPreferences().LanguageColorOverrides.Should().ContainSingle(entry => entry.Key == "Tradeband" && entry.Color == "#123456");

        handler.Handle(player, "langcolor", "tradeband", "default").Status.Should().Be(EnumCommandStatus.Success);
        player.GetChatVisualPreferences().LanguageColorOverrides.Should().BeEmpty();
    }

    [Fact]
    public void Handle_LanguageColorStatusDoesNotClearExistingOverride()
    {
        var player = CreatePlayer();
        var handler = CreateHandler();
        handler.Handle(player, "langcolor", "tr", "#123456");

        handler.Handle(player, "langcolor", "tr", null).Status.Should().Be(EnumCommandStatus.Success);

        player.GetChatVisualPreferences().LanguageColorOverrides.Should().ContainSingle(entry => entry.Key == "Tradeband" && entry.Color == "#123456");
    }

    [Fact]
    public void Handle_GlobalOocColorHonorsGlobalOocConfigGate()
    {
        var player = CreatePlayer();
        var config = CreateConfig();
        config.EnableGlobalOOC = false;
        var handler = CreateHandler(config);

        var result = handler.Handle(player, "gooccolor", "#123456", null);

        result.Status.Should().Be(EnumCommandStatus.Error);
        player.GetChatVisualPreferences().GlobalOocColorOverride.Should().BeNull();
    }

    [Fact]
    public void Handle_ResetClearsPersistedPreferenceState()
    {
        var player = CreatePlayer();
        var handler = CreateHandler();
        handler.Handle(player, "labels", "on", null);

        handler.Handle(player, "reset", null, null).Status.Should().Be(EnumCommandStatus.Success);

        player.GetChatVisualPreferences().ShowLanguageLabels.Should().BeFalse();
    }

    private static ChatPreferencesCommandHandler CreateHandler(ModConfig? config = null)
    {
        return new ChatPreferencesCommandHandler(config ?? CreateConfig(), ResolveLanguage);
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static Language ResolveLanguage(string identifier)
    {
        return identifier?.Equals("tr", StringComparison.OrdinalIgnoreCase) == true
            || identifier?.Equals("tradeband", StringComparison.OrdinalIgnoreCase) == true
            ? new Language("Tradeband", "A trade language", "tr", ["tar"], "#D4A96A", false, false)
            : null!;
    }

    private static IServerPlayer CreatePlayer()
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns("player-1");
        player.PlayerName.Returns("Alice");
        var modData = new Dictionary<string, byte[]>();
        player.GetModdata(Arg.Any<string>()).Returns(call => modData.TryGetValue(call.Arg<string>(), out var value) ? value : null);
        player.When(call => call.SetModdata(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call =>
            {
                var key = call.ArgAt<string>(0);
                var value = call.ArgAt<byte[]>(1);
                if (value == null)
                {
                    modData.Remove(key);
                    return;
                }

                modData[key] = value;
            });
        player.When(call => call.RemoveModdata(Arg.Any<string>()))
            .Do(call => modData.Remove(call.ArgAt<string>(0)));
        return player;
    }
}
