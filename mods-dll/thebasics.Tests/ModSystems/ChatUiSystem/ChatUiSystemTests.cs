using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using thebasics.Configs;
using ChatUiModSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection(thebasics.Tests.ModSystems.AnalyticsServiceTestCollection.Name)]
public class ChatUiSystemTests
{
    [Theory]
    [InlineData("NotesRequests", "OnNotesViewMessage")]
    [InlineData("LanguageRequests", "OnLanguageConfigResultMessage")]
    [InlineData("LanguageRequests", "OnLanguageConfigOpenMessage")]
    public void UntrackedView_DoesNotConsumePendingEditorRequest(string trackerName, string handler)
    {
        var tracker = GetStaticField<thebasics.ModSystems.ChatUiSystem.DialogRequestTracker>(trackerName);
        var id = tracker.Begin(() => { }, (_, _) => { });
        object message = handler switch
        {
            "OnNotesViewMessage" => new thebasics.ModSystems.Notes.Models.TheBasicsNotesViewMessage(),
            "OnLanguageConfigOpenMessage" => new thebasics.Models.TheBasicsLanguageConfigOpenMessage(),
            _ => new thebasics.Models.TheBasicsLanguageConfigResultMessage()
        };
        try
        {
            InvokeStaticMethod(handler, message);
            tracker.Accept(id).Should().BeTrue();
        }
        finally
        {
            tracker.Reset();
        }
    }

    [Fact]
    public void SentSaveWithoutReply_ExpiresAndIgnoresLateResponse()
    {
        var previousApi = GetStaticField<ICoreClientAPI>("_api");
        var previousChannel = GetStaticField<SafeClientNetworkChannel>("_safeNetworkChannel");
        var api = Substitute.For<ICoreClientAPI>();
        Action<float> expire = null!;
        api.Event.RegisterCallback(Arg.Do<Action<float>>(callback => expire = callback), 30000);
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safe = new SafeClientNetworkChannel(channel, api);
        try
        {
            SetStaticField("_api", api);
            SetStaticField("_safeNetworkChannel", safe);
            var request = new thebasics.Models.CharacterSheetSaveRequest();
            InvokeStaticMethod("SendCharacterSheetSaveRequest", request);
            channel.Received().SendPacket(request);
            GetStaticField<bool>("_pendingCharacterSheetSave").Should().BeTrue();
            expire(30);
            GetStaticField<bool>("_pendingCharacterSheetSave").Should().BeFalse();
            InvokeStaticMethod("OnCharacterSheetViewMessage", new thebasics.Models.CharacterSheetViewMessage { IsSaveResponse = true, RequestId = request.RequestId });
            _ = api.DidNotReceive().World;
        }
        finally
        {
            InvokeStaticMethod("OnCharacterSheetDialogClosed");
            SetStaticField("_api", previousApi);
            SetStaticField("_safeNetworkChannel", previousChannel);
        }
    }

    [Fact]
    public void StaleAutoOpenResponse_DoesNotReplaceCachedSheetOrTitle()
    {
        var previousApi = GetStaticField<ICoreClientAPI>("_api");
        var previousCache = GetStaticField<thebasics.Models.CharacterSheetViewMessage>("_lastOwnCharacterSheetView");
        var previousTitle = GetStaticField<string>("_characterDialogTitleOverride");
        try
        {
            var api = Substitute.For<ICoreClientAPI>();
            SetStaticField("_api", api);
            var current = new thebasics.Models.CharacterSheetViewMessage { TargetPlayerUid = "player", DisplayName = "New" };
            SetStaticField("_lastOwnCharacterSheetView", current);
            SetStaticField("_characterDialogTitleOverride", "New");
            SetStaticField("_pendingCharacterSheetAutoOpenRequestId", 0L);
            InvokeStaticMethod("OnCharacterSheetViewMessage", new thebasics.Models.CharacterSheetViewMessage { AutoOpenRequestId = 123, TargetPlayerUid = "player", DisplayName = "Old" });
            GetStaticField<thebasics.Models.CharacterSheetViewMessage>("_lastOwnCharacterSheetView").Should().BeSameAs(current);
            GetStaticField<string>("_characterDialogTitleOverride").Should().Be("New");
            _ = api.DidNotReceive().World;
        }
        finally
        {
            SetStaticField("_api", previousApi);
            SetStaticField("_lastOwnCharacterSheetView", previousCache);
            SetStaticField("_characterDialogTitleOverride", previousTitle);
        }
    }

    [Fact]
    public void FailedAutoOpenWithoutCachedSheet_AllowsRetry()
    {
        var previousConfig = GetStaticField<ModConfig>("_config");
        try
        {
            SetStaticField("_config", new ModConfig { EnableCharacterSheets = true });
            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            InvokeStaticMethod("OnCharacterDialogComposed");
            GetStaticField<bool>("_characterDialogSheetAutoOpenHandled").Should().BeFalse();
        }
        finally
        {
            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            SetStaticField("_config", previousConfig);
        }
    }

    [Fact]
    public void ExplicitOpen_SupersedesPendingAutoOpenState()
    {
        var previousChannel = GetStaticField<SafeClientNetworkChannel>("_safeNetworkChannel");
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safe = new SafeClientNetworkChannel(channel, Substitute.For<ICoreClientAPI>());
        try
        {
            SetStaticField("_safeNetworkChannel", safe);
            SetStaticField("_pendingCharacterSheetAutoOpenRequestId", 123L);
            SetStaticField("_pendingCharacterSheetOpenFromCharacterDialog", true);
            SetStaticField("_characterDialogSheetAutoOpenHandled", true);
            SetStaticField("_characterSheetOpenedFromCharacterDialog", true);

            InvokeStaticMethod("SendCharacterSheetRequest", new thebasics.Models.CharacterSheetOpenRequest());

            GetStaticField<long>("_pendingCharacterSheetAutoOpenRequestId").Should().Be(0);
            GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeFalse();
            GetStaticField<bool>("_characterDialogSheetAutoOpenHandled").Should().BeTrue();
            GetStaticField<bool>("_characterSheetOpenedFromCharacterDialog").Should().BeFalse();
            InvokeStaticMethod("OnCharacterDialogComposed");
            channel.Received(1).SendPacket(Arg.Any<thebasics.Models.CharacterSheetOpenRequest>());
        }
        finally
        {
            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            SetStaticField("_safeNetworkChannel", previousChannel);
        }
    }

    [Theory]
    [InlineData(true, "admin")]
    [InlineData(false, "view")]
    public void HeadshotRefresh_PreservesCurrentMode(bool admin, string expectedMode)
    {
        var previousLocale = Lang.CurrentLocale;
        var translations = Substitute.For<Vintagestory.API.Config.ITranslationService>();
        translations.HasTranslation(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        Lang.AvailableLanguages["test-headshot"] = translations;
        Lang.ChangeLanguage("test-headshot");
        var previousChannel = GetStaticField<SafeClientNetworkChannel>("_safeNetworkChannel");
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safe = new SafeClientNetworkChannel(channel, Substitute.For<ICoreClientAPI>());
        var dialog = (thebasics.ModSystems.ChatUiSystem.CharacterSheetDialog)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(thebasics.ModSystems.ChatUiSystem.CharacterSheetDialog));
        typeof(thebasics.ModSystems.ChatUiSystem.CharacterSheetDialog).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dialog, new thebasics.Models.CharacterSheetViewMessage { TargetPlayerUid = "player", IsAdminView = admin });
        try
        {
            SetStaticField("_characterSheetDialog", dialog);
            SetStaticField("_safeNetworkChannel", safe);
            InvokeStaticMethod("OnHeadshotUploadResult", new thebasics.Models.HeadshotUploadResult { Success = true, TargetPlayerUid = "player" });
            channel.Received().SendPacket(Arg.Is<thebasics.Models.CharacterSheetOpenRequest>(request => request.Mode == expectedMode && request.TargetPlayerUid == "player"));
        }
        finally
        {
            SetStaticField<object?>("_characterSheetDialog", null);
            SetStaticField("_safeNetworkChannel", previousChannel);
            Lang.ChangeLanguage(previousLocale);
            Lang.AvailableLanguages.Remove("test-headshot");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(456)]
    public void DismissedAutoOpenResponse_DoesNotReopenSheet(long newerRequestId)
    {
        SetStaticField("_pendingCharacterSheetOpenFromCharacterDialog", true);
        InvokeStaticMethod("OnCharacterSheetDialogClosed");
        SetStaticField("_pendingCharacterSheetAutoOpenRequestId", newerRequestId);
        var response = new thebasics.Models.CharacterSheetViewMessage { AutoOpenRequestId = 123 };
        var handler = typeof(ChatUiModSystem).GetMethod("OnCharacterSheetViewMessage", BindingFlags.Static | BindingFlags.NonPublic)!;

        var receive = () => handler.Invoke(null, new object[] { response });

        receive.Should().NotThrow();
        GetStaticField<object>("_characterSheetDialog").Should().BeNull();
        GetStaticField<long>("_pendingCharacterSheetAutoOpenRequestId").Should().Be(newerRequestId);
        ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
    }

    [Fact]
    public void CharacterTabRecomposition_AfterSheetDismissed_DoesNotRequestAnotherSheet()
    {
        var previousConfig = GetStaticField<ModConfig>("_config");
        var previousChannel = GetStaticField<SafeClientNetworkChannel>("_safeNetworkChannel");
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safe = new SafeClientNetworkChannel(channel, Substitute.For<ICoreClientAPI>());
        try
        {
            SetStaticField("_safeNetworkChannel", safe);
            SetStaticField("_config", new ModConfig { EnableCharacterSheets = true });
            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            InvokeStaticMethod("OnCharacterDialogComposed");
            GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeTrue();
            GetStaticField<long>("_pendingCharacterSheetAutoOpenRequestId").Should().BePositive();

            InvokeStaticMethod("OnCharacterSheetDialogClosed");
            GetStaticField<long>("_pendingCharacterSheetAutoOpenRequestId").Should().Be(0);
            InvokeStaticMethod("OnCharacterDialogComposed");
            GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeFalse();
            InvokeStaticMethod("OnCharacterDialogComposed");
            GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeFalse();

            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            InvokeStaticMethod("OnCharacterDialogComposed");
            GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeTrue();
        }
        finally
        {
            ChatUiModSystem.GuiDialogCharacter_OnGuiClosed_Postfix();
            SetStaticField("_config", previousConfig);
            SetStaticField("_safeNetworkChannel", previousChannel);
        }
    }

    [Theory]
    [InlineData(50, 30, 50)]
    [InlineData(20, 30, 20)]
    [InlineData(30, 0, 30)]
    [InlineData(-1, 30, 0)]
    public void GetEffectiveTypingIndicatorRange_UsesConfiguredTypingRange(
        int typingRange,
        int nametagRange,
        int expectedRange)
    {
        var config = new ModConfig
        {
            TypingIndicatorMaxRange = typingRange,
            NametagRenderRange = nametagRange,
        };

        var range = ChatUiModSystem.GetEffectiveTypingIndicatorRange(config);

        range.Should().Be(expectedRange);
    }

    [Fact]
    public void CharacterSheetDialogClosed_ClearsCharacterDialogAutoOpenState()
    {
        SetStaticField("_pendingCharacterSheetOpenFromCharacterDialog", true);
        SetStaticField("_characterSheetOpenedFromCharacterDialog", true);

        InvokeStaticMethod("OnCharacterSheetDialogClosed");

        GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeFalse();
        GetStaticField<bool>("_characterSheetOpenedFromCharacterDialog").Should().BeFalse();
    }

    private static void SetStaticField<T>(string name, T value)
    {
        var field = GetStaticFieldInfo(name);
        field.SetValue(null, value);
    }

    private static T GetStaticField<T>(string name)
    {
        var field = GetStaticFieldInfo(name);
        return (T)field.GetValue(null)!;
    }

    private static FieldInfo GetStaticFieldInfo(string name)
    {
        return typeof(ChatUiModSystem).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ChatUiModSystem).FullName, name);
    }

    private static void InvokeStaticMethod(string name, params object[] arguments)
    {
        var method = typeof(ChatUiModSystem).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ChatUiModSystem).FullName, name);
        method.Invoke(null, arguments);
    }
}
