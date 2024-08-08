using HarmonyLib;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
    
    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        _api = api;
        
        RegisterForServerSideConfig();
        
        if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
            _api.Logger.Debug("THEBASICS - Patching!");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }
    }

    private void RegisterForServerSideConfig()
    {
        _clientChannel = _api.Network.RegisterChannel("thebasics_config")
            .RegisterMessageType<TheBasicsNetworkMessage>()
            .SetMessageHandler<TheBasicsNetworkMessage>(OnServerMessage);
    }

    private void OnServerMessage(TheBasicsNetworkMessage networkMessage)
    {
        _preventProximityChannelSwitching = networkMessage.PreventProximityChannelSwitching;
        _proximityGroupId = networkMessage.ProximityGroupId;
    }
    
    // private static HudDialogChat GetChatHudElement(ICoreClientAPI api)
    // {
    //     return api.Gui.LoadedGuis.First(gui => gui.GetType() == typeof(HudDialogChat)) as HudDialogChat;
    // }
    
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