using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.RpCharacters;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.RpCharacters;

[Collection(AnalyticsServiceTestCollection.Name)]
public class RpCharacterServiceTests : IDisposable
{
    public RpCharacterServiceTests()
    {
        AnalyticsService.Shutdown();
    }

    public void Dispose()
    {
        AnalyticsService.Shutdown();
    }

    [Fact]
    public void TrackLanguageStateInvariantFailure_WhenBabbleIsDefaultAndNoLanguagesAreKnown_DoesNotReportFailure()
    {
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(sink, allowErrorTelemetry: true);
        var player = CreatePlayer();
        player.SetLanguages([]);
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", LanguageSystem.BabbleLang.Name);

        player.TrackLanguageStateInvariantFailure("character_restore");

        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void RestoreProjection_WhenDefaultLanguageIsNotKnown_ReportsSanitizedLanguageStateFailure()
    {
        const string rawPlayerUid = "raw-player-uid";
        const string pseudonymousPlayerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(
            sink,
            allowErrorTelemetry: true,
            playerPseudonymizer: playerUid => playerUid == rawPlayerUid ? pseudonymousPlayerId : null);
        var player = CreatePlayer(rawPlayerUid);
        var service = new RpCharacterService(CreateConfig());

        service.RestoreProjection(player, new RpCharacterProjectionSnapshot
        {
            Languages = ["Common"],
            DefaultLanguage = "UnknownLanguage"
        });

        var analyticsEvent = sink.Events.Should().ContainSingle().Subject;
        analyticsEvent.Name.Should().Be("mod failure");
        analyticsEvent.Properties.Should().ContainKey("area").WhoseValue.Should().Be("language_state");
        analyticsEvent.Properties.Should().ContainKey("operation").WhoseValue.Should().Be("character_restore");
        analyticsEvent.Properties.Should().ContainKey("severity").WhoseValue.Should().Be("warning");
        analyticsEvent.Properties.Should().ContainKey("result").WhoseValue.Should().Be("default_language_unknown");
        analyticsEvent.Properties.Should().ContainKey("recovered").WhoseValue.Should().Be(false);
        analyticsEvent.Properties.Should().ContainKey("pseudonymous_player_id").WhoseValue.Should().Be(pseudonymousPlayerId);
        analyticsEvent.Properties.Values.Should().NotContain(rawPlayerUid);
        analyticsEvent.Properties.Values.Should().NotContain("UnknownLanguage");
    }

    [Fact]
    public void GetModData_WhenKnownLanguagesCannotDeserialize_ReportsSanitizedLanguageStateFailure()
    {
        const string rawPlayerUid = "raw-player-uid";
        const string pseudonymousPlayerId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(
            sink,
            allowErrorTelemetry: true,
            playerPseudonymizer: playerUid => playerUid == rawPlayerUid ? pseudonymousPlayerId : null);
        var api = Substitute.For<ICoreAPI>();
        api.Logger.Returns(Substitute.For<ILogger>());
        var player = new FakeServerPlayer(rawPlayerUid)
        {
            Entity = new EntityPlayer { Api = api }
        };
        player.SetModdata("BASIC_LANGUAGES", [255]);

        IServerPlayerExtensions.GetModData(player, "BASIC_LANGUAGES", new List<string>()).Should().BeEmpty();

        player.GetModdata("BASIC_LANGUAGES").Should().BeNull();
        var analyticsEvent = sink.Events.Should().ContainSingle().Subject;
        analyticsEvent.Name.Should().Be("mod failure");
        analyticsEvent.Properties.Should().ContainKey("area").WhoseValue.Should().Be("language_state");
        analyticsEvent.Properties.Should().ContainKey("operation").WhoseValue.Should().Be("read_known_languages");
        analyticsEvent.Properties.Should().ContainKey("severity").WhoseValue.Should().Be("warning");
        analyticsEvent.Properties.Should().ContainKey("result").WhoseValue.Should().Be("decode_failed");
        analyticsEvent.Properties.Should().ContainKey("recovered").WhoseValue.Should().Be(true);
        analyticsEvent.Properties.Should().ContainKey("pseudonymous_player_id").WhoseValue.Should().Be(pseudonymousPlayerId);
        analyticsEvent.Properties.Should().NotContainKey("exception_message");
        analyticsEvent.Properties.Should().NotContainKey("exception_type");
        analyticsEvent.Properties.Values.Should().NotContain(rawPlayerUid);
    }

    [Fact]
    public void GetModData_WhenDefaultLanguageCannotDeserialize_ReportsSanitizedLanguageStateFailure()
    {
        const string rawPlayerUid = "raw-player-uid";
        const string pseudonymousPlayerId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(
            sink,
            allowErrorTelemetry: true,
            playerPseudonymizer: playerUid => playerUid == rawPlayerUid ? pseudonymousPlayerId : null);
        var api = Substitute.For<ICoreAPI>();
        api.Logger.Returns(Substitute.For<ILogger>());
        var player = new FakeServerPlayer(rawPlayerUid)
        {
            Entity = new EntityPlayer { Api = api }
        };
        player.SetModdata("BASIC_DEFAULT_LANGUAGE", [255]);

        IServerPlayerExtensions.GetModData(player, "BASIC_DEFAULT_LANGUAGE", string.Empty).Should().BeEmpty();

        player.GetModdata("BASIC_DEFAULT_LANGUAGE").Should().BeNull();
        var analyticsEvent = sink.Events.Should().ContainSingle().Subject;
        analyticsEvent.Name.Should().Be("mod failure");
        analyticsEvent.Properties.Should().ContainKey("area").WhoseValue.Should().Be("language_state");
        analyticsEvent.Properties.Should().ContainKey("operation").WhoseValue.Should().Be("read_default_language");
        analyticsEvent.Properties.Should().ContainKey("severity").WhoseValue.Should().Be("warning");
        analyticsEvent.Properties.Should().ContainKey("result").WhoseValue.Should().Be("decode_failed");
        analyticsEvent.Properties.Should().ContainKey("recovered").WhoseValue.Should().Be(true);
        analyticsEvent.Properties.Should().ContainKey("pseudonymous_player_id").WhoseValue.Should().Be(pseudonymousPlayerId);
        analyticsEvent.Properties.Should().NotContainKey("exception_message");
        analyticsEvent.Properties.Should().NotContainKey("exception_type");
        analyticsEvent.Properties.Values.Should().NotContain(rawPlayerUid);
    }

    [Fact]
    public void EnsureRegistry_CreatesDefaultCharacterFromCurrentProjection()
    {
        var player = CreatePlayer();
        var config = CreateConfig();
        var service = new RpCharacterService(config);
        StoreSheet(player, "summary", "Quiet and watchful");
        player.SetNicknameColor("#112233");
        player.SetNametagBackgroundColor("#223344");
        player.SetNametagBorderColor("#334455");
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
        character.Projection.NametagBackgroundColor.Should().Be("#223344");
        character.Projection.NametagBorderColor.Should().Be("#334455");
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
            NametagBackgroundColor = "#556677",
            NametagBorderColor = "#667788",
            Languages = ["Tradeband"],
            DefaultLanguage = "Tradeband",
            ChatMode = ProximityChatMode.Yell,
            ChatterEnabled = false
        };
        IServerPlayerExtensions.SetModData(player, RpCharacterService.CharacterSlotsKey, registry);

        StoreSheet(player, "summary", "Edited Alice");
        player.SetNicknameColor("#112233");
        player.SetNametagBackgroundColor("#223344");
        player.SetNametagBorderColor("#334455");
        player.SetLanguages(["Common"]);
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", "Common");
        player.SetChatMode(ProximityChatMode.Whisper);
        player.SetChatterEnabled(true);

        var selectResult = service.SelectCharacter(player, bob.CharacterId);

        selectResult.Success.Should().BeTrue();
        service.GetActiveCharacterId(player).Should().Be(bob.CharacterId);
        GetStoredSheetData(player).Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Bob summary");
        player.GetNicknameColor().Should().Be("#445566");
        player.GetNametagBackgroundColor().Should().Be("#556677");
        player.GetNametagBorderColor().Should().Be("#667788");
        player.GetLanguages().Should().Equal("Tradeband");
        player.GetDefaultLanguageName().Should().Be("Tradeband");
        player.GetChatMode().Should().Be(ProximityChatMode.Yell);
        player.GetChatterEnabled().Should().BeFalse();

        var savedRegistry = service.ReadRegistry(player);
        var savedAlice = savedRegistry.Characters.Should().ContainSingle(character => character.CharacterId == aliceId).Subject;
        savedAlice.Projection.Sheet.Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Edited Alice");
        savedAlice.Projection.NicknameColor.Should().Be("#112233");
        savedAlice.Projection.NametagBackgroundColor.Should().Be("#223344");
        savedAlice.Projection.NametagBorderColor.Should().Be("#334455");
        savedAlice.Projection.Languages.Should().Equal("Common");
        savedAlice.Projection.DefaultLanguage.Should().Be("Common");
        savedAlice.Projection.ChatMode.Should().Be(ProximityChatMode.Whisper);
        savedAlice.Projection.ChatterEnabled.Should().BeTrue();
    }

    [Fact]
    public void SelectCharacter_CapturesAndRestoresRegisteredParticipantSnapshots()
    {
        var player = CreatePlayer();
        var participant = new TestModDataParticipant();
        var service = new RpCharacterService(CreateConfig(), participants: new[] { participant });
        IServerPlayerExtensions.SetModData(player, TestModDataParticipant.ModDataKey, "Alice original");
        service.EnsureRegistry(player);
        var aliceId = service.GetActiveCharacterId(player);
        service.CreateCharacter(player, "Bob", maxCharacters: 3).Success.Should().BeTrue();

        var registry = service.ReadRegistry(player);
        var bob = registry.Characters.Should().ContainSingle(character => character.DisplayName == "Bob").Subject;
        bob.SetExtensionSnapshot(TestModDataParticipant.ParticipantCode, SerializerUtil.Serialize("Bob state"));
        IServerPlayerExtensions.SetModData(player, RpCharacterService.CharacterSlotsKey, registry);
        IServerPlayerExtensions.SetModData(player, TestModDataParticipant.ModDataKey, "Alice edited");

        var result = service.SelectCharacter(player, bob.CharacterId);

        result.Success.Should().BeTrue();
        IServerPlayerExtensions.GetModData(player, TestModDataParticipant.ModDataKey, string.Empty).Should().Be("Bob state");
        var savedAlice = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.CharacterId == aliceId).Subject;
        SerializerUtil.Deserialize<string>(savedAlice.GetExtensionSnapshot(TestModDataParticipant.ParticipantCode)).Should().Be("Alice edited");
    }

    [Fact]
    public void RecreatedService_RestoresPersistedCharacterStateAfterReconnect()
    {
        var player = CreatePlayer();
        var participant = new TestModDataParticipant();
        var firstConnection = new RpCharacterService(CreateConfig(), participants: new[] { participant });
        IServerPlayerExtensions.SetModData(player, TestModDataParticipant.ModDataKey, "Alice state");
        firstConnection.EnsureRegistry(player);
        firstConnection.CreateCharacter(player, "Bob", maxCharacters: 3).Success.Should().BeTrue();
        var registry = firstConnection.ReadRegistry(player);
        var bob = registry.Characters
            .Should().ContainSingle(character => character.DisplayName == "Bob").Subject;
        bob.SetExtensionSnapshot(TestModDataParticipant.ParticipantCode, SerializerUtil.Serialize("Bob state"));
        IServerPlayerExtensions.SetModData(player, RpCharacterService.CharacterSlotsKey, registry);

        var reconnectedService = new RpCharacterService(CreateConfig(), participants: new[] { participant });
        var result = reconnectedService.SelectCharacter(player, bob.CharacterId);

        result.Success.Should().BeTrue();
        IServerPlayerExtensions.GetModData(player, TestModDataParticipant.ModDataKey, string.Empty).Should().Be("Bob state");
        var alice = reconnectedService.ReadRegistry(player).Characters
            .Should().ContainSingle(character => character.DisplayName == "Alice").Subject;
        SerializerUtil.Deserialize<string>(alice.GetExtensionSnapshot(TestModDataParticipant.ParticipantCode))
            .Should().Be("Alice state");
    }

    [Fact]
    public void SelectCharacter_DoesNotPrepareParticipantsWhenValidationFails()
    {
        var player = CreatePlayer();
        var preparingParticipant = new TestPreparingParticipant();
        var service = new RpCharacterService(CreateConfig(), participants: new IRpCharacterSwitchParticipant[]
        {
            preparingParticipant,
            new TestRejectingParticipant()
        });
        service.EnsureRegistry(player);
        service.CreateCharacter(player, "Bob", maxCharacters: 3).Success.Should().BeTrue();
        var bob = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.DisplayName == "Bob").Subject;

        var result = service.SelectCharacter(player, bob.CharacterId);

        result.Success.Should().BeFalse();
        preparingParticipant.PrepareCalls.Should().Be(0);
    }

    [Fact]
    public void SelectCharacter_SavesCapturedActiveSnapshotAfterRollbackSucceeds()
    {
        var player = CreatePlayer();
        var service = new RpCharacterService(CreateConfig(), participants: new[] { new TestThrowingRestoreParticipant() });
        IServerPlayerExtensions.SetModData(player, TestThrowingRestoreParticipant.ModDataKey, "Alice original");
        service.EnsureRegistry(player);
        var aliceId = service.GetActiveCharacterId(player);
        service.CreateCharacter(player, "Bob", maxCharacters: 3).Success.Should().BeTrue();

        var registry = service.ReadRegistry(player);
        var bob = registry.Characters.Should().ContainSingle(character => character.DisplayName == "Bob").Subject;
        bob.SetExtensionSnapshot(TestThrowingRestoreParticipant.ParticipantCode, SerializerUtil.Serialize(TestThrowingRestoreParticipant.ThrowValue));
        IServerPlayerExtensions.SetModData(player, RpCharacterService.CharacterSlotsKey, registry);
        IServerPlayerExtensions.SetModData(player, TestThrowingRestoreParticipant.ModDataKey, "Alice edited");

        var result = service.SelectCharacter(player, bob.CharacterId);

        result.Success.Should().BeFalse();
        IServerPlayerExtensions.GetModData(player, TestThrowingRestoreParticipant.ModDataKey, string.Empty).Should().Be("Alice edited");
        var savedAlice = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.CharacterId == aliceId).Subject;
        SerializerUtil.Deserialize<string>(savedAlice.GetExtensionSnapshot(TestThrowingRestoreParticipant.ParticipantCode)).Should().Be("Alice edited");
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

    [Fact]
    public void InventoryParticipant_DoesNotRestoreLegacyRecordWithoutInventorySnapshot()
    {
        var record = new RpCharacterRecord
        {
            SnapshotVersion = 1,
            Inventory = new RpCharacterInventorySnapshot()
        };

        RpCharacterInventoryParticipant.HasRestorableSnapshot(record).Should().BeFalse();
    }

    [Fact]
    public void RpCharacterRecord_DefaultSnapshotVersionIsLegacySafe()
    {
        new RpCharacterRecord().SnapshotVersion.Should().Be(0);
    }

    [Fact]
    public void InventoryParticipant_RestoresEmptyInventorySnapshotForNewRecords()
    {
        var record = new RpCharacterRecord
        {
            SnapshotVersion = 2,
            Inventory = new RpCharacterInventorySnapshot { Available = true }
        };

        RpCharacterInventoryParticipant.HasRestorableSnapshot(record).Should().BeTrue();
    }

    [Fact]
    public void CreateCharacter_MarksEmptyInventoryAvailableWhenInventoryParticipantIsRegistered()
    {
        var player = CreatePlayer();
        var config = CreateConfig();
        var service = new RpCharacterService(config, participants: new[] { new RpCharacterInventoryParticipant() });
        service.EnsureRegistry(player);

        service.CreateCharacter(player, "Bob", maxCharacters: 3).Success.Should().BeTrue();

        var bob = service.ReadRegistry(player).Characters.Should().ContainSingle(character => character.DisplayName == "Bob").Subject;
        bob.SnapshotVersion.Should().Be(2);
        bob.Inventory.Available.Should().BeTrue();
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
        return new FakeServerPlayer(uid, name);
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

    private sealed class TestModDataParticipant : IRpCharacterSwitchParticipant
    {
        public const string ParticipantCode = "test:moddata";
        public const string ModDataKey = "TEST_CHARACTER_STATE";

        public string Code => ParticipantCode;

        public int Order => 100;

        public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
        {
            return RpCharacterOperationResult.Ok(string.Empty);
        }

        public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
            var value = IServerPlayerExtensions.GetModData(context.Player, ModDataKey, string.Empty);
            record.SetExtensionSnapshot(Code, SerializerUtil.Serialize(value));
        }

        public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
            var data = record.GetExtensionSnapshot(Code);
            IServerPlayerExtensions.SetModData(context.Player, ModDataKey, SerializerUtil.Deserialize<string>(data) ?? string.Empty);
        }
    }

    private sealed class TestPreparingParticipant : IRpCharacterSwitchParticipant, IRpCharacterSwitchPreparationParticipant
    {
        public int PrepareCalls { get; private set; }

        public string Code => "test:prepare";

        public int Order => 100;

        public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
        {
            return RpCharacterOperationResult.Ok(string.Empty);
        }

        public RpCharacterOperationResult Prepare(RpCharacterSwitchContext context)
        {
            PrepareCalls++;
            return RpCharacterOperationResult.Ok(string.Empty);
        }

        public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
        }

        public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
        }
    }

    private sealed class TestRejectingParticipant : IRpCharacterSwitchParticipant
    {
        public string Code => "test:reject";

        public int Order => 200;

        public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
        {
            return RpCharacterOperationResult.Error("nope");
        }

        public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
        }

        public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
        }
    }

    private sealed class TestThrowingRestoreParticipant : IRpCharacterSwitchParticipant
    {
        public const string ParticipantCode = "test:rollback";
        public const string ModDataKey = "TEST_ROLLBACK_STATE";
        public const string ThrowValue = "throw";

        public string Code => ParticipantCode;

        public int Order => 100;

        public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
        {
            return RpCharacterOperationResult.Ok(string.Empty);
        }

        public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
            var value = IServerPlayerExtensions.GetModData(context.Player, ModDataKey, string.Empty);
            record.SetExtensionSnapshot(Code, SerializerUtil.Serialize(value));
        }

        public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
        {
            var value = SerializerUtil.Deserialize<string>(record.GetExtensionSnapshot(Code)) ?? string.Empty;
            if (value == ThrowValue)
            {
                throw new InvalidOperationException("restore failed");
            }

            IServerPlayerExtensions.SetModData(context.Player, ModDataKey, value);
        }
    }

    private sealed class RecordingAnalyticsSink : IAnalyticsSink
    {
        public List<RecordedEvent> Events { get; } = new();

        public bool IsEnabled => true;

        public void Track(string eventName, IDictionary<string, object> properties)
        {
            Events.Add(new RecordedEvent(eventName, new Dictionary<string, object>(properties)));
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed record RecordedEvent(string Name, IDictionary<string, object> Properties);
}
