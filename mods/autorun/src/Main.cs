using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace autorun
{
    public class Main : ModSystem
    {
        public const string HotkeyCode = "egocarib_MapMarkerGUI";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.Input.RegisterHotKey(
                hotkeyCode: HotkeyCode,
                name: Lang.Get("autorun:config-keybind-name"),
                key: GlKeys.W,
                type: HotkeyType.CharacterControls,
                altPressed: false, ctrlPressed: true, shiftPressed: false);
            api.Input.

            api.Input.SetHotKeyHandler(hotkeyCode: HotkeyCode, handler: ToggleGUI);
        }
    }
}