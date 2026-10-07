using FluentAssertions;
using HarmonyLib;
using thebasics.Configs;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using ChatUiModSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ChatTabLayoutTests
{
    [Fact]
    public void InstalledGameSupportsCompositionPatchAndUnreadTextureRefresh()
    {
        AccessTools.Method(typeof(GuiElementHorizontalTabs), "ComposeOverlays", [typeof(bool)]).Should().NotBeNull();
        var original = AccessTools.Method(typeof(HudDialogChat), "ComposeChatGuis");
        var postfix = AccessTools.Method(typeof(ChatUiModSystem), nameof(ChatUiModSystem.OnChatTabsComposed));
        var harmony = new Harmony("thebasics.tests.chat-tab-layout");
        try
        {
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            Harmony.GetPatchInfo(original).Postfixes.Should().Contain(patch => patch.PatchMethod == postfix);
        }
        finally
        {
            harmony.Unpatch(original, postfix);
        }
    }

    [Theory]
    [InlineData("GameOrder", 0, -5, -6, -1, 11, 12, 42)]
    [InlineData("First", 42, 0, -5, -6, -1, 11, 12)]
    [InlineData("AfterGeneral", 0, 42, -5, -6, -1, 11, 12)]
    [InlineData("unknown", 0, -5, -6, -1, 11, 12, 42)]
    public void Arrange_OnlyMovesProximityAndKeepsTheSharedArray(string position, params int[] expected)
    {
        var gameOrder = Tabs(0, -5, -6, -1, 11, 12, 42);
        var tabs = gameOrder.ToArray();
        var sharedArray = tabs;
        var config = new ModConfig { ProximityChatTabPosition = position };

        ChatTabLayout.Arrange(tabs, gameOrder, config, 42);

        tabs.Should().BeSameAs(sharedArray);
        tabs.Select(tab => tab.DataInt).Should().Equal(expected);
        gameOrder.Select(tab => tab.DataInt).Should().Equal(0, -5, -6, -1, 11, 12, 42);
        ChatTabLayout.Arrange(tabs, gameOrder, config, 42).Should().BeFalse();
        config.ProximityChatTabPosition = "GameOrder";
        ChatTabLayout.Arrange(tabs, gameOrder, config, 42);
        tabs.Should().Equal(gameOrder);
    }

    [Theory]
    [InlineData(true, 42)]
    [InlineData(false, 999)]
    [InlineData(false, null)]
    public void Arrange_DoesNotPinInGeneralModeOrWithoutTheProximityGroup(bool generalMode, int? proximityId)
    {
        var gameOrder = Tabs(0, -5, 11, 42);
        var tabs = gameOrder.ToArray();
        ChatTabLayout.Arrange(tabs, gameOrder,
            new ModConfig { UseGeneralChannelAsProximityChat = generalMode, ProximityChatTabPosition = "First" }, proximityId)
            .Should().BeFalse();
        tabs.Should().Equal(gameOrder);
    }

    [Theory]
    [InlineData(true, true, 11, 42, 11)]
    [InlineData(true, true, 999, 42, 42)]
    [InlineData(true, false, 999, 42, 0)]
    [InlineData(true, true, 999, 999, 0)]
    [InlineData(false, true, 11, 42, 42)]
    [InlineData(false, false, 11, 42, 0)]
    [InlineData(true, true, null, 0, 0)]
    public void OpeningGroup_UsesValidRememberedTabThenConfiguredDefaultThenGeneral(
        bool remember, bool proximityDefault, int? rememberedId, int? proximityId, int expected)
    {
        var config = new ModConfig { PreserveDefaultChatChoice = remember, ProximityChatAsDefault = proximityDefault };
        ChatTabLayout.OpeningGroup(Tabs(0, -5, 11, 42), config, rememberedId, proximityId).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OpeningGroup_GeneralModeIgnoresOldProximitySelectionEvenIfItsTabStillExists(bool remember, bool proximityDefault)
    {
        var config = new ModConfig
        {
            UseGeneralChannelAsProximityChat = true,
            PreserveDefaultChatChoice = remember,
            ProximityChatAsDefault = proximityDefault
        };
        ChatTabLayout.OpeningGroup(Tabs(0, -5, 42), config, 42, 42).Should().Be(GlobalConstants.GeneralChatGroup);
    }

    private static GuiTab[] Tabs(params int[] ids) => ids.Select(id => new GuiTab { DataInt = id, Name = id.ToString() }).ToArray();
}
