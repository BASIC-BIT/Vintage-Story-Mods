using System.Collections.Generic;
using HarmonyLib;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;

namespace thebasics.ModSystems.ChatUiSystem;

[HarmonyPatch]
public class ChatUiSystem : ModSystem
{
    private static ICoreClientAPI _api;
    private Harmony _harmony;
    private static int _proximityGroupId;
    private static bool _preventProximityChannelSwitching;
    private static IClientNetworkChannel _clientChannel;

    private Dictionary<string, string> PlayerNicknames = new Dictionary<string, string>();
    
    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Event.OnEntitySpawn += ApplyNickname;
        _api = api;
        
        RegisterForServerSideConfig();
        
        if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
            _api.Logger.Debug("THEBASICS - Patching!");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }
    }

    private void ApplyNickname(Entity entity)
    {
        if (entity is EntityPlayer entityPlayer)
        {
            if (PlayerNicknames.ContainsKey(entityPlayer.PlayerUID))
            {
                SetPlayerNickname(entityPlayer, PlayerNicknames[entityPlayer.PlayerUID]);
            }
        }
    }

    private void RegisterForServerSideConfig()
    {
        _clientChannel = _api.Network.RegisterChannel("thebasics_config")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage)
            .RegisterMessageType<TheBasicsPlayerNicknameMessage>()
            .SetMessageHandler<TheBasicsPlayerNicknameMessage>(OnServerPlayerNicknameMessage);
    }

    private void OnServerPlayerNicknameMessage(TheBasicsPlayerNicknameMessage packet)
    {
        PlayerNicknames[packet.PlayerUID] = packet.Nickname;

        var player = _api.World.PlayerByUid(packet.PlayerUID);

        if (player == null)
        {
            _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find player by UUID");
            return;
        }

        var entity = player.Entity;
        
        if (entity == null)
        {
            _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find entity for player");
            return;
        }
        
        SetPlayerNickname(entity, packet.Nickname);

        // TODO: Check if player is currently being rendered to update nickname
    }

    private void SetPlayerNickname(EntityPlayer entityPlayer, string name)
    {
        _api.Logger.Debug($"THEBASICS - Setting player ${entityPlayer.Player.PlayerName} nametag to nickname ${name}");
        var nametag = entityPlayer.GetBehavior<EntityBehaviorNameTag>();

        nametag.SetName(name);
    }

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
    public static bool HandleGotoGroupPacket()
    {
        _api.Logger.Debug("THEBASICS - Handling GotoGroupPacket");
        var game = (ClientMain)_api.World;
        if (_preventProximityChannelSwitching && game.currentGroupid == _proximityGroupId)
        {
            _api.Logger.Debug("THEBASICS - Denying GotoGroupPacket");
            return false;
        }
        
        _api.Logger.Debug("THEBASICS - Allowing GotoGroupPacket");
        return true;
    }
    
    public override void Dispose() {
        _harmony?.UnpatchAll(Mod.Info.ModID);
    }
}