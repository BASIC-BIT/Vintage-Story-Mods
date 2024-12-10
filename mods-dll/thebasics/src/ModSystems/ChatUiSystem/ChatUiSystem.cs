using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Cairo;
using HarmonyLib;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

[HarmonyPatch]
public class ChatUiSystem : ModSystem
{
    private static ICoreClientAPI _api;
    private Harmony _harmony;
    private static int _proximityGroupId;
    private static bool _preventProximityChannelSwitching;

    private static IClientNetworkChannel _clientConfigChannel;

    // private static IClientNetworkChannel _clientNicknameChannel;
    private GuiDialogCharacterBase dlg;
    private GuiDialog.DlgComposers Composers => this.dlg.Composers;

    // private Dictionary<string, string> PlayerNicknames = new Dictionary<string, string>();

    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        _api = api;

        // _api.Event.OnEntityLoaded += ApplyNickname;
        // _api.Event.OnEntitySpawn += ApplyRenderer;

        api.RegisterEntityRendererClass("TestPlayerShape", typeof(RpTextEntityPlayerShapeRenderer));

        RegisterForServerSideConfig();

        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            _api.Logger.Debug("THEBASICS - Patching!");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }

        this.dlg =
            api.Gui.LoadedGuis.Find((Predicate<GuiDialog>)(dlg => dlg is GuiDialogCharacterBase)) as
                GuiDialogCharacterBase;
        // this.dlg.ComposeExtraGuis += Dlg_ComposeExtraGuis;

        // api.Event.IsPlayerReady += OnPlayerReady;
    }

    private void Dlg_ComposeExtraGuis()
    {
        ComposeCharacterSheetGui();
    }

    private void ComposeCharacterSheetGui()
    {

        var pcDialogPadding = 400;
        var dialogWidth = 400;
        var rowPadding = 20;
        ElementBounds statsBounds = this.Composers["playercharacter"].Bounds;
        _api.Logger.Debug($"THEBASICS - test bounds {statsBounds.absX + statsBounds.OuterWidth + pcDialogPadding}, {statsBounds.absY}");
        ElementBounds dialogBounds = new ElementBounds()
            .WithSizing(ElementSizing.Fixed)
            .WithFixedPosition(statsBounds.absX + statsBounds.OuterWidth + pcDialogPadding, statsBounds.absY)
            .WithFixedSize(dialogWidth, statsBounds.OuterHeight);

        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren();
            
        this.Composers["charsheet"] = _api.Gui
            .CreateCompo("charsheet", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("Character Sheet"), () => dlg.OnTitleBarClose())
            .BeginChildElements()
            // .AddInset()
                .AddDynamicText("Testing!", CairoFont.WhiteSmallText(), ElementStdBounds.Rowed(0, rowPadding))
            .EndChildElements()
            .Compose();
    }

    // private void ApplyRenderer(Entity entity)
    // {
    //     if (entity is EntityPlayer entityPlayer)
    //     {
    //         var oldRenderer = entity.Properties.Client.Renderer;
    //         var newRenderer = new RpTextEntityPlayerShapeRenderer(entity,_api);
    //         entity.Properties.Client.Renderer = newRenderer;
    //         entity.Properties.Client.RendererName = "TestPlayerShape";
    //
    //         oldRenderer.Dispose();
    //         newRenderer.OnEntityLoaded();
    //     }
    // }

    // private bool OnPlayerReady(ref EnumHandling handling)
    // {
    //     handling = EnumHandling.PassThrough;
    //
    //     if (_api.World.Player != null && _api.World.Player.Entity != null)
    //     {
    //         _api.Logger.Debug($"THEBASICS - OnPlayerReady, Setting my player nickname!");
    //         ApplyNickname(_api.World.Player.Entity);
    //     }
    //     else
    //     {
    //         _api.Logger.Debug($"THEBASICS - OnPlayerReady, Player or Entity was null!");
    //     }
    //
    //     return true;
    // }

    // private void ApplyNickname(Entity entity)
    // {
    //     if (entity is EntityPlayer entityPlayer)
    //     {
    //         if (PlayerNicknames.ContainsKey(entityPlayer.PlayerUID))
    //         {
    //             SetPlayerNickname(entityPlayer, PlayerNicknames[entityPlayer.PlayerUID]);
    //         }
    //     }
    // }

    private void RegisterForServerSideConfig()
    {
        _clientConfigChannel = _api.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage);
        // _clientNicknameChannel = _api.Network.RegisterChannel("thebasics_nickname")
        //     .RegisterMessageType<TheBasicsPlayerNicknameMessage>()
        //     .SetMessageHandler<TheBasicsPlayerNicknameMessage>(OnServerPlayerNicknameMessage);
    }

    // private void OnServerPlayerNicknameMessage(TheBasicsPlayerNicknameMessage packet)
    // {
    //     PlayerNicknames[packet.PlayerUID] = packet.Nickname;
    //
    //     var player = _api.World.PlayerByUid(packet.PlayerUID);
    //
    //     if (player == null)
    //     {
    //         _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find player by UUID");
    //         return;
    //     }
    //
    //     var entity = player.Entity;
    //     
    //     if (entity == null)
    //     {
    //         _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find entity for player");
    //         return;
    //     }
    //     
    //     SetPlayerNickname(entity, packet.Nickname);
    //
    //     // TODO: Check if player is currently being rendered to update nickname
    // }

    // private void SetPlayerNickname(EntityPlayer entityPlayer, string name)
    // {
    //     _api.Logger.Debug($"THEBASICS - Setting player ${entityPlayer.Player.PlayerName} nametag to nickname ${name}");
    //     var nametag = entityPlayer.GetBehavior<EntityBehaviorNameTag>();
    //
    //     nametag.SetName(name);
    // }

    private void OnServerConfigMessage(TheBasicsConfigMessage configMessage)
    {
        _preventProximityChannelSwitching = configMessage.PreventProximityChannelSwitching;
        _proximityGroupId = configMessage.ProximityGroupId;
    }

    /*
     * Prevent chat switching by preventing original method from executing if user is currently viewing the proximity tab and server config is set to prevent switching
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "HandleGotoGroupPacket")]
    public static bool HandleGotoGroupPacket(HudDialogChat __instance, Packet_Server packet)
    {
        var game = (ClientMain)_api.World;
        int gotoGroupId = packet.GotoGroup.GroupId;
        _api.Logger.Debug(
            $"THEBASICS - Handling GotoGroupPacket ~ Current group: {game.currentGroupid} GotoGroup: {gotoGroupId}");
        if (_preventProximityChannelSwitching && game.currentGroupid == _proximityGroupId)
        {
            _api.Logger.Debug("THEBASICS - Denying GotoGroupPacket");
            return false;
        }

        _api.Logger.Debug("THEBASICS - Allowing GotoGroupPacket");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityShapeRenderer), "OnChatMessage")]
    protected static bool OnChatMessage(EntityPlayerShapeRenderer __instance, int groupId, string message,
        EnumChatType chattype, string data)
    {
        if (!(__instance.entity is EntityPlayer))
        {
            return true;
        }

        if (data == null || !data.Contains("from:") ||
            __instance.entity.Pos.SquareDistanceTo(__instance.capi.World.Player.Entity.Pos.XYZ) >= 400.0 ||
            message.Length <= 0)
            return false;
        string[] strArray1 = data.Split(new char[1] { ',' }, 2);
        if (strArray1.Length < 2)
            return false;
        string[] strArray2 = strArray1[0].Split(new char[1]
        {
            ':'
        }, 2);
        string[] strArray3 = strArray1[1].Split(new char[1]
        {
            ':'
        }, 2);
        if (strArray2[0] != "from")
            return false;
        int result;
        int.TryParse(strArray2[1], out result);
        if (__instance.entity.EntityId != (long)result)
            return false;
        message = strArray3[1];
        message = message.Replace("&lt;", "<").Replace("&gt;", ">");
        LoadedTexture loadedTexture = __instance.capi.Gui.TextTexture.GenTextTexture(message,
            new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble), 350, new TextBackground()
            {
                FillColor = GuiStyle.DialogLightBgColor,
                Padding = 100,
                Radius = GuiStyle.ElementBGRadius
            }, EnumTextOrientation.Center);
        var messageTexturesField = __instance.GetType()
            .GetField("messageTextures", BindingFlags.NonPublic | BindingFlags.Instance);
        var messageTextures = (List<MessageTexture>)messageTexturesField.GetValue(__instance);
        messageTextures.Insert(0, new MessageTexture()
        {
            tex = loadedTexture,
            message = message,
            receivedTime = __instance.capi.World.ElapsedMilliseconds
        });

        return false;
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll(Mod.Info.ModID);
    }

    public void SetupCharacterDialog()
    {
    }
}