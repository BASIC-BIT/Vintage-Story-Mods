using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.CharacterSheets.Models;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.CharacterSheets;

public class CharacterSheetSystemTests
{
    [Fact]
    public void GetMissingRequiredFieldLabels_ReturnsMissingRequiredFields()
    {
        var player = CreatePlayer();
        var config = CreateConfig(
            new CharacterSheetFieldDefinition { Id = "nickname", Label = "Nickname", Optional = false, BindTo = "thebasics.nickname" },
            new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", Optional = false },
            new CharacterSheetFieldDefinition { Id = "appearance", Label = "Appearance", Optional = true }
        );
        player.SetNickname("Alice");

        var missingFields = CharacterSheetSystem.GetMissingRequiredFieldLabels(player, config);

        missingFields.Should().Equal("First Impression");
    }

    [Fact]
    public void GetMissingRequiredFieldLabels_IgnoresAdminOnlyFields()
    {
        var player = CreatePlayer();
        var config = CreateConfig(
            new CharacterSheetFieldDefinition
            {
                Id = "moderationNote",
                Label = "Moderation Note",
                Optional = false,
                Visibility = CharacterSheetFieldVisibilities.Admin
            }
        );

        var missingFields = CharacterSheetSystem.GetMissingRequiredFieldLabels(player, config);

        missingFields.Should().BeEmpty();
    }

    private static ModConfig CreateConfig(params CharacterSheetFieldDefinition[] fields)
    {
        return new ModConfig
        {
            EnableCharacterSheets = true,
            CharacterSheetRequireRequiredFieldsForRoleplay = true,
            CharacterSheetFields = new List<CharacterSheetFieldDefinition>(fields)
        };
    }

    private static IServerPlayer CreatePlayer()
    {
        var player = Substitute.For<IServerPlayer>();
        var modData = new Dictionary<string, byte[]>();
        player.GetModdata(Arg.Any<string>()).Returns(call => modData.TryGetValue(call.Arg<string>(), out var value) ? value : null);
        player.When(call => call.SetModdata(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call => modData[call.ArgAt<string>(0)] = call.ArgAt<byte[]>(1));
        return player;
    }
}
