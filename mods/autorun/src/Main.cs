using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace autorun
{
    public class Main : ModSystem
    {
        public const string HotkeyCode = "autorun_autorun";

        private ICoreClientAPI _api;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            _api = api;
            api.Input.RegisterHotKey(
                hotkeyCode: HotkeyCode,
                name: "Autorun",
                key: GlKeys.BracketRight,
                type: HotkeyType.CharacterControls,
                altPressed: false, ctrlPressed: false, shiftPressed: false);

            api.Input.SetHotKeyHandler(hotkeyCode: HotkeyCode, handler: OnAutorunHotkey);

            api.Input.InWorldAction += Input_InWorldAction;
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
        }

        private bool OnAutorunHotkey(KeyCombination key)
        {
            // _api.World.Player.Entity.WalkYaw = _api.World.Player.CameraYaw;
            _api.World.Player.WorldData.EntityControls.Forward = true;
            return false;
        }
    }
}