using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.RpCharacters;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.RpCharacters;

public class RpCharacterServiceTests
{
    [Fact]
    public void EnsureRegistry_CreatesDefaultCharacterFromCurrentProjection()
    {
        var player = CreatePlayer();
        var config = CreateConfig();
        var service = new RpCharacterService(config);
        StoreSheet(player, "summary", "Quiet and watchful");
        player.SetNicknameColor("#112233");
        player.SetLanguages(["Common", "Tradeband"]);
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", "Tradeband");
        player.SetChatMode(ProximityChatMode.Whisper);
        player.SetChatterEnabled(false);

        var registry = service.EnsureRegistry(player);

        registry.Characters.Should().ContainSingle();
        var character = registry.Characters[0];
        service.GetActiveCharacterId(player).Should().Be(character.CharacterId);
        character.DisplayName.Should().Be("Alice");
        character.Projection.Sheet.Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Quiet and watchful");
        character.Projection.NicknameColor.Should().Be("#112233");
        character.Projection.Languages.Should().Equal("Common", "Tradeband");
        character.Projection.DefaultLanguage.Should().Be("Tradeband");
        character.Projection.ChatMode.Should().Be(ProximityChatMode.Whisper);
        character.Projection.ChatterEnabled.Should().BeFalse();
    }

    [Fact]
    public void SelectCharacter_CapturesActiveProjectionAndRestoresTargetProjection()
    {
        var player = CreatePlayer();
        var config = CreateConfig();
        var service = new RpCharacterService(config);
        StoreSheet(player, "summary", "Original Alice");
        var registry = service.EnsureRegistry(player);
        var aliceId = service.GetActiveCharacterId(player);
        var createResult = service.CreateCharacter(player, "Bob", maxCharacters: 3);
        createResult.Success.Should().BeTrue();

        registry = service.ReadRegistry(player);
        var bob = registry.Characters.Should().ContainSingle(character => character.DisplayName == "Bob").Subject;
        bob.Projection = new RpCharacterProjectionSnapshot
        {
            Sheet = new CharacterSheetData
            {
                Fields = new List<CharacterSheetStoredField>
                {
                    new CharacterSheetStoredField { FieldId = "summary", Value = "Bob summary" }
                }
            },
            NicknameColor = "#445566",
            Languages = ["Tradeband"],
            DefaultLanguage = "Tradeband",
            ChatMode = ProximityChatMode.Yell,
            ChatterEnabled = false
        };
        IServerPlayerExtensions.SetModData(player, RpCharacterService.CharacterSlotsKey, registry);

        StoreSheet(player, "summary", "Edited Alice");
        player.SetNicknameColor("#112233");
        player.SetLanguages(["Common"]);
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", "Common");
        player.SetChatMode(ProximityChatMode.Whisper);
        player.SetChatterEnabled(true);

        var selectResult = service.SelectCharacter(player, bob.CharacterId);

        selectResult.Success.Should().BeTrue();
        service.GetActiveCharacterId(player).Should().Be(bob.CharacterId);
        GetStoredSheetData(player).Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Bob summary");
        player.GetNicknameColor().Should().Be("#445566");
        player.GetLanguages().Should().Equal("Tradeband");
        player.GetDefaultLanguageName().Should().Be("Tradeband");
        player.GetChatMode().Should().Be(ProximityChatMode.Yell);
        player.GetChatterEnabled().Should().BeFalse();

        var savedRegistry = service.ReadRegistry(player);
        var savedAlice = savedRegistry.Characters.Should().ContainSingle(character => character.CharacterId == aliceId).Subject;
        savedAlice.Projection.Sheet.Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Edited Alice");
        savedAlice.Projection.NicknameColor.Should().Be("#112233");
        savedAlice.Projection.Languages.Should().Equal("Common");
        savedAlice.Projection.DefaultLanguage.Should().Be("Common");
        savedAlice.Projection.ChatMode.Should().Be(ProximityChatMode.Whisper);
        savedAlice.Projection.ChatterEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreateCharacter_EnforcesActiveCharacterLimit()
    {
        var player = CreatePlayer();
        var service = new RpCharacterService(CreateConfig());
        service.EnsureRegistry(player);

        var result = service.CreateCharacter(player, "Bob", maxCharacters: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("maximum 1");
    }

    [Fact]
    public void SelectCharacter_AcceptsUnambiguousCharacterIdPrefix()
    {
        var player = CreatePlayer();
        var service = new RpCharacterService(CreateConfig());
        service.EnsureRegistry(player);
        service.CreateCharacter(player, "Bob Smith", maxCharacters: 3).Success.Should().BeTrue();
        var bob = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.DisplayName == "Bob Smith").Subject;

        var result = service.SelectCharacter(player, "bob-smith");

        result.Success.Should().BeTrue();
        service.GetActiveCharacterId(player).Should().Be(bob.CharacterId);
    }

    [Fact]
    public void SelectCharacter_IgnoresWrappedQuoteCharactersDuringLookup()
    {
        var player = CreatePlayer();
        var service = new RpCharacterService(CreateConfig());
        service.EnsureRegistry(player);
        service.CreateCharacter(player, "Bob Smith", maxCharacters: 3).Success.Should().BeTrue();
        var bob = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.DisplayName == "Bob Smith").Subject;

        var result = service.SelectCharacter(player, "\"Bob Smith\"");

        result.Success.Should().BeTrue();
        service.GetActiveCharacterId(player).Should().Be(bob.CharacterId);
    }

    [Fact]
    public void SelectCharacter_IgnoresAccidentalStoredQuoteCharactersDuringLookup()
    {
        var player = CreatePlayer();
        var service = new RpCharacterService(CreateConfig());
        service.EnsureRegistry(player);
        service.CreateCharacter(player, "\"Bob Smith", maxCharacters: 3).Success.Should().BeTrue();
        var bob = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.DisplayName == "\"Bob Smith").Subject;

        var result = service.SelectCharacter(player, "Bob Smith");

        result.Success.Should().BeTrue();
        service.GetActiveCharacterId(player).Should().Be(bob.CharacterId);
    }

    private static ModConfig CreateConfig()
    {
        return new ModConfig
        {
            EnableCharacterSheets = true,
            EnableRpCharacterSlots = true,
            Languages = new List<Language>
            {
                new Language("Common", "The universal language", "c", ["al"], "#E9DDCE", true, false),
                new Language("Tradeband", "A trade language", "tr", ["tar"], "#D4A96A", false, false)
            }
        };
    }

    private static IServerPlayer CreatePlayer(string uid = "player-1", string name = "Alice")
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);
        player.PlayerName.Returns(name);
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
                }
                else
                {
                    modData[key] = value;
                }
            });
        player.When(call => call.RemoveModdata(Arg.Any<string>()))
            .Do(call => modData.Remove(call.ArgAt<string>(0)));
        return player;
    }

    private static void StoreSheet(IServerPlayer player, string fieldId, string value)
    {
        IServerPlayerExtensions.SetModData(player, "BASIC_CHARACTER_SHEET", new CharacterSheetData
        {
            Fields = new List<CharacterSheetStoredField>
            {
                new CharacterSheetStoredField { FieldId = fieldId, Value = value }
            }
        });
    }

    private static CharacterSheetData GetStoredSheetData(IServerPlayer player)
    {
        return SerializerUtil.Deserialize(player.GetModdata("BASIC_CHARACTER_SHEET"), new CharacterSheetData()) ?? new CharacterSheetData();
    }
}
