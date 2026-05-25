using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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

    [Fact]
    public void GetMissingRequiredFieldLabels_WhenRoleplayRequirementDisabled_ReturnsEmpty()
    {
        var player = CreatePlayer();
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", Optional = false });
        config.CharacterSheetRequireRequiredFieldsForRoleplay = false;

        var missingFields = CharacterSheetSystem.GetMissingRequiredFieldLabels(player, config);

        missingFields.Should().BeEmpty();
    }

    [Fact]
    public void BuildClientView_ForOwnSheet_ReturnsEditableNonAdminFields()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        player.SetModdata("BASIC_CHARACTER_SHEET", SerializerUtil.Serialize(new CharacterSheetData
        {
            Fields = new List<CharacterSheetStoredField>
            {
                new CharacterSheetStoredField { FieldId = "summary", Value = "Quiet and watchful" },
                new CharacterSheetStoredField { FieldId = "adminnote", Value = "hidden" }
            }
        }));
        var system = new CharacterSheetSystem
        {
            Config = CreateConfig(
                new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", Optional = false },
                new CharacterSheetFieldDefinition { Id = "adminnote", Label = "Admin Note", Visibility = CharacterSheetFieldVisibilities.Admin }
            )
        };

        var view = system.BuildClientView(player, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeOwn });

        view.Success.Should().BeTrue();
        view.CanEdit.Should().BeTrue();
        view.Fields.Should().ContainSingle();
        view.Fields[0].FieldId.Should().Be("summary");
        view.Fields[0].Value.Should().Be("Quiet and watchful");
        view.Fields[0].CanEdit.Should().BeTrue();
    }

    [Fact]
    public void BuildClientView_WhenCharacterSheetsDisabled_ReturnsDisabledErrorCode()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" });
        config.EnableCharacterSheets = false;
        var system = CreateSystem(player, config);

        var view = system.BuildClientView(player, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeOwn });

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        view.ErrorCode.Should().Be(CharacterSheetViewMessage.ErrorCodeDisabled);
    }

    [Fact]
    public void SaveClientFields_WhenCharacterSheetsDisabled_ReturnsDisabledErrorCode()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" });
        config.EnableCharacterSheets = false;
        var system = CreateSystem(player, config);

        var view = system.SaveClientFields(player, CreateSaveRequest("summary", "Quiet and watchful"));

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        view.ErrorCode.Should().Be(CharacterSheetViewMessage.ErrorCodeDisabled);
    }

    [Fact]
    public void BuildClientView_ForViewWithoutTarget_ReturnsError()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" }));

        var view = system.BuildClientView(player, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeView });

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        view.Message.Should().Be("thebasics:charsheet-error-view-target");
    }

    [Fact]
    public void BuildClientView_ForViewTarget_ReturnsReadOnlyTargetFields()
    {
        EnsureLangInitialized();
        var viewer = CreatePlayer("player-1", "Alice");
        var target = CreatePlayer("player-2", "Bob");
        target.SetModdata("BASIC_CHARACTER_SHEET", SerializerUtil.Serialize(new CharacterSheetData
        {
            Fields = new List<CharacterSheetStoredField>
            {
                new CharacterSheetStoredField { FieldId = "summary", Value = "Quiet and watchful" }
            }
        }));
        var system = CreateSystem(viewer, target, CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" }));

        var view = system.BuildClientView(viewer, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeView, TargetPlayerUid = target.PlayerUID });

        view.Success.Should().BeTrue();
        view.CanEdit.Should().BeFalse();
        view.TargetPlayerUid.Should().Be(target.PlayerUID);
        view.Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Quiet and watchful" && !field.CanEdit);
    }

    [Fact]
    public void SaveClientFields_ForOwnSheet_StoresSubmittedFieldAndReturnsEditableView()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" }));

        var view = system.SaveClientFields(player, CreateSaveRequest("summary", "Quiet and watchful"));

        view.Success.Should().BeTrue();
        view.IsSaveResponse.Should().BeTrue();
        view.CanEdit.Should().BeTrue();
        view.Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Quiet and watchful");
        GetStoredSheetData(player).Fields.Should().ContainSingle(field => field.FieldId == "summary" && field.Value == "Quiet and watchful");
    }

    [Fact]
    public void BuildClientView_ForOptionField_ReturnsOptionChoices()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition
        {
            Id = "demeanor",
            Label = "Demeanor",
            Type = CharacterSheetFieldTypes.Option,
            Options = ["Calm", "Guarded", "Hostile"]
        }));

        var view = system.BuildClientView(player, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeOwn });

        view.Success.Should().BeTrue();
        view.Fields.Should().ContainSingle();
        view.Fields[0].Type.Should().Be(CharacterSheetFieldTypes.Option);
        view.Fields[0].Options.Should().Equal("Calm", "Guarded", "Hostile");
        view.Fields[0].CanEdit.Should().BeTrue();
    }

    [Fact]
    public void BuildClientView_ForLongStringField_ReturnsEditorRows()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition
        {
            Id = "background",
            Label = "Background",
            Type = CharacterSheetFieldTypes.LongString,
            EditorRows = 8
        }));

        var view = system.BuildClientView(player, new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeOwn });

        view.Success.Should().BeTrue();
        view.Fields.Should().ContainSingle();
        view.Fields[0].EditorRows.Should().Be(8);
    }

    [Fact]
    public void NameTransformer_ForRoleplayName_UsesFullNameWhenNicknameIsMissing()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "fullName", Label = "Full Name" });
        var system = CreateSystem(player, config);
        system.SaveClientFields(player, CreateSaveRequest("fullName", "Dame Alice"));
        var transformer = new NameTransformer(new RPProximityChatSystem { Config = config });

        var formattedName = transformer.GetFormattedName(player, isIC: true, config);

        formattedName.Should().Be("Dame Alice");
    }

    [Fact]
    public void NameTransformer_ForRoleplayName_UsesSheetBackedNicknameBeforeFullName()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(
            new CharacterSheetFieldDefinition { Id = "fullName", Label = "Full Name", BindTo = "thebasics.fullName" },
            new CharacterSheetFieldDefinition { Id = "nickname", Label = "Nickname", BindTo = "thebasics.nickname" }
        );
        var system = CreateSystem(player, config);
        system.SaveClientFields(player, CreateSaveRequest("fullName", "Dame Alice"));
        system.SaveClientFields(player, CreateSaveRequest("nickname", "Ally"));
        var transformer = new NameTransformer(new RPProximityChatSystem { Config = config });

        var formattedName = transformer.GetFormattedName(player, isIC: true, config);

        formattedName.Should().Be("Ally");
        GetStoredSheetData(player).Fields.Should().Contain(field => field.FieldId == "nickname" && field.Value == "Ally");
    }

    [Fact]
    public void NameTransformer_ForRoleplayName_FallsBackToLegacyNickname()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "nickname", Label = "Nickname", BindTo = "thebasics.nickname" });
        player.SetNickname("Legacy Ally");
        var transformer = new NameTransformer(new RPProximityChatSystem { Config = config });

        var formattedName = transformer.GetFormattedName(player, isIC: true, config);

        formattedName.Should().Be("Legacy Ally");
    }

    [Fact]
    public void SetNickname_WithCharacterSheetsEnabled_StoresNicknameInSheetAndClearsLegacyValue()
    {
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "nickname", Label = "Nickname", BindTo = "thebasics.nickname" });
        player.SetNickname("Legacy Ally");

        player.SetNickname("Sheet Ally", config);

        player.GetNickname(config).Should().Be("Sheet Ally");
        player.GetNickname().Should().Be("Alice");
        GetStoredSheetData(player).Fields.Should().ContainSingle(field => field.FieldId == "nickname" && field.Value == "Sheet Ally");
    }

    [Fact]
    public void NameTransformer_ForRoleplayName_UsesConfiguredFullNameBinding()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "legalName", Label = "Legal Name", BindTo = "thebasics.fullName" });
        var system = CreateSystem(player, config);
        system.SaveClientFields(player, CreateSaveRequest("legalName", "Dame Alice"));
        var transformer = new NameTransformer(new RPProximityChatSystem { Config = config });

        var formattedName = transformer.GetFormattedName(player, isIC: true, config);

        formattedName.Should().Be("Dame Alice");
    }

    [Fact]
    public void BuildNametagDisplayName_UsesFullNameWhenNicknameIsMissing()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "fullName", Label = "Full Name" });
        var system = CreateSystem(player, config);
        system.SaveClientFields(player, CreateSaveRequest("fullName", "Dame Alice"));

        var displayName = CharacterSheetSystem.BuildNametagDisplayName(player, new ModConfig
        {
            ShowNicknameInNametag = true,
            ShowPlayerNameInNametag = true
        });

        displayName.Should().Be("Dame Alice (Alice)");
    }

    [Fact]
    public void BuildNametagDisplayName_UsesConfiguredFullNameBinding()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "legalName", Label = "Legal Name", BindTo = "thebasics.fullName" });
        var system = CreateSystem(player, config);
        system.SaveClientFields(player, CreateSaveRequest("legalName", "Dame Alice"));
        config.ShowNicknameInNametag = true;
        config.ShowPlayerNameInNametag = true;

        var displayName = CharacterSheetSystem.BuildNametagDisplayName(player, config);

        displayName.Should().Be("Dame Alice (Alice)");
    }

    [Fact]
    public void BuildNametagDisplayName_DoesNotDuplicatePlayerNameWhenNoNicknameOrFullNameExists()
    {
        var player = CreatePlayer("player-1", "Alice");

        var displayName = CharacterSheetSystem.BuildNametagDisplayName(player, new ModConfig
        {
            ShowNicknameInNametag = true,
            ShowPlayerNameInNametag = true
        });

        displayName.Should().Be("Alice");
    }

    [Fact]
    public void SaveClientFields_ForAdminSheet_WithPrivilege_StoresTargetField()
    {
        EnsureLangInitialized();
        var admin = CreatePlayer("admin-1", "Admin");
        var target = CreatePlayer("player-1", "Alice");
        admin.HasPrivilege("commandplayer").Returns(true);
        var system = CreateSystem(admin, target, CreateConfig(new CharacterSheetFieldDefinition { Id = "adminnote", Label = "Admin Note", Visibility = CharacterSheetFieldVisibilities.Admin }));

        var view = system.SaveClientFields(admin, CreateSaveRequest("adminnote", "Needs review", target.PlayerUID, isAdminAction: true));

        view.Success.Should().BeTrue();
        view.IsAdminView.Should().BeTrue();
        view.Fields.Should().ContainSingle(field => field.FieldId == "adminnote" && field.Value == "Needs review" && field.CanEdit);
        GetStoredSheetData(target).Fields.Should().ContainSingle(field => field.FieldId == "adminnote" && field.Value == "Needs review");
    }

    [Fact]
    public void SaveClientFields_ForAdminSheet_WithoutPrivilege_ReturnsError()
    {
        EnsureLangInitialized();
        var editor = CreatePlayer("editor-1", "Editor");
        var target = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(editor, target, CreateConfig(new CharacterSheetFieldDefinition { Id = "adminnote", Label = "Admin Note", Visibility = CharacterSheetFieldVisibilities.Admin }));

        var view = system.SaveClientFields(editor, CreateSaveRequest("adminnote", "Needs review", target.PlayerUID, isAdminAction: true));

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        GetStoredSheetData(target).Fields.Should().BeEmpty();
    }

    [Fact]
    public void SaveClientFields_ForOwnSheet_RejectsAdminOnlyField()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition { Id = "adminnote", Label = "Admin Note", Visibility = CharacterSheetFieldVisibilities.Admin }));

        var view = system.SaveClientFields(player, CreateSaveRequest("adminnote", "hidden"));

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        GetStoredSheetData(player).Fields.Should().BeEmpty();
    }

    [Fact]
    public void SaveClientFields_WithInvalidOption_ReturnsError()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition
        {
            Id = "demeanor",
            Label = "Demeanor",
            Type = CharacterSheetFieldTypes.Option,
            Options = ["Calm", "Guarded"]
        }));

        var view = system.SaveClientFields(player, CreateSaveRequest("demeanor", "Hostile"));

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        GetStoredSheetData(player).Fields.Should().BeEmpty();
    }

    [Fact]
    public void SaveClientFields_ForOptionalOption_WithEmptyValue_ClearsStoredValue()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition
        {
            Id = "demeanor",
            Label = "Demeanor",
            Type = CharacterSheetFieldTypes.Option,
            Optional = true,
            Options = ["Calm", "Guarded"]
        }));
        system.SaveClientFields(player, CreateSaveRequest("demeanor", "Calm"));

        var view = system.SaveClientFields(player, CreateSaveRequest("demeanor", ""));

        view.Success.Should().BeTrue();
        GetStoredSheetData(player).Fields.Should().BeEmpty();
    }

    [Fact]
    public void NicknameRequirementTransformer_WhenCharacterSheetRequiredFieldIsMissing_StopsRoleplayMessage()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", Optional = false });
        var transformer = new NicknameRequirementTransformer(new RPProximityChatSystem { Config = config });
        var context = new MessageContext { SendingPlayer = player };
        context.SetFlag(MessageContext.IS_ROLEPLAY);

        transformer.ShouldTransform(context).Should().BeTrue();
        var transformedContext = transformer.Transform(context);

        transformedContext.State.Should().Be(MessageContextState.STOP);
        player.Received(1).SendMessage(Arg.Any<int>(), "thebasics:charsheet-required-warning", EnumChatType.CommandError);
    }

    [Fact]
    public void NicknameRequirementTransformer_WhenCharacterSheetRequirementDisabled_DoesNotRequireNickname()
    {
        var player = CreatePlayer("player-1", "Alice");
        var config = CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", Optional = false });
        config.CharacterSheetRequireRequiredFieldsForRoleplay = false;
        var transformer = new NicknameRequirementTransformer(new RPProximityChatSystem { Config = config });
        var context = new MessageContext { SendingPlayer = player };
        context.SetFlag(MessageContext.IS_ROLEPLAY);

        transformer.ShouldTransform(context).Should().BeFalse();
    }

    [Fact]
    public void SaveClientFields_WithLaterInvalidField_DoesNotPartiallySaveEarlierField()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(
            new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression" },
            new CharacterSheetFieldDefinition
            {
                Id = "demeanor",
                Label = "Demeanor",
                Type = CharacterSheetFieldTypes.Option,
                Options = ["Calm", "Guarded"]
            }
        ));

        var request = CreateSaveRequest("summary", "Quiet and watchful");
        request.Fields.Add(new CharacterSheetFieldValueMessage { FieldId = "demeanor", Value = "Hostile" });

        var view = system.SaveClientFields(player, request);

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        GetStoredSheetData(player).Fields.Should().BeEmpty();
    }

    [Fact]
    public void SaveClientFields_WithTooLongValue_ReturnsError()
    {
        EnsureLangInitialized();
        var player = CreatePlayer("player-1", "Alice");
        var system = CreateSystem(player, CreateConfig(new CharacterSheetFieldDefinition { Id = "summary", Label = "First Impression", MaxLength = 3 }));

        var view = system.SaveClientFields(player, CreateSaveRequest("summary", "abcd"));

        view.Success.Should().BeFalse();
        view.IsErrorResponse.Should().BeTrue();
        GetStoredSheetData(player).Fields.Should().BeEmpty();
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

    private static IServerPlayer CreatePlayer(string uid = "player-1", string name = "Alice")
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);
        player.PlayerName.Returns(name);
        var modData = new Dictionary<string, byte[]>();
        player.GetModdata(Arg.Any<string>()).Returns(call => modData.TryGetValue(call.Arg<string>(), out var value) ? value : null);
        player.When(call => call.SetModdata(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call => modData[call.ArgAt<string>(0)] = call.ArgAt<byte[]>(1));
        player.When(call => call.RemoveModdata(Arg.Any<string>()))
            .Do(call => modData.Remove(call.ArgAt<string>(0)));
        return player;
    }

    private static CharacterSheetSystem CreateSystem(IServerPlayer player, ModConfig config)
    {
        return CreateSystem(player, player, config);
    }

    private static CharacterSheetSystem CreateSystem(IServerPlayer editor, IServerPlayer target, ModConfig config)
    {
        return new CharacterSheetSystem
        {
            API = CreateApi(editor, target),
            Config = config
        };
    }

    private static ICoreServerAPI CreateApi(params IServerPlayer[] players)
    {
        var api = Substitute.For<ICoreServerAPI>();
        var server = Substitute.For<IServerAPI>();
        var playerData = Substitute.For<IPlayerDataManager>();
        server.Players.Returns(players);
        api.Server.Returns(server);
        api.PlayerData.Returns(playerData);
        playerData.PlayerDataByUid.Returns(new Dictionary<string, IServerPlayerData>());
        api.Logger.Returns(Substitute.For<ILogger>());
        return api;
    }

    private static CharacterSheetSaveRequest CreateSaveRequest(string fieldId, string value, string targetPlayerUid = "", bool isAdminAction = false)
    {
        var request = new CharacterSheetSaveRequest
        {
            TargetPlayerUid = targetPlayerUid,
            IsAdminAction = isAdminAction
        };
        request.Fields.Add(new CharacterSheetFieldValueMessage { FieldId = fieldId, Value = value });
        return request;
    }

    private static CharacterSheetData GetStoredSheetData(IServerPlayer player)
    {
        return SerializerUtil.Deserialize(player.GetModdata("BASIC_CHARACTER_SHEET"), new CharacterSheetData()) ?? new CharacterSheetData();
    }

    private static void EnsureLangInitialized()
    {
        var translationService = Substitute.For<ITranslationService>();
        translationService.Get(Arg.Any<string>(), Arg.Any<object[]>()).Returns(call => call.ArgAt<string>(0));
        Lang.AvailableLanguages["en"] = translationService;
        Lang.ChangeLanguage("en");
    }
}
