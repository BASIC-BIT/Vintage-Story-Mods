using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem
{
    public class RpTextModSystem : ModSystem
    {
        private Harmony harmony;
        private ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            // Initialize Harmony
            harmony = new Harmony("com.thebasics.rptext");
            harmony.PatchAll(typeof(RpTextEntityPlayerShapeRendererPatch).Assembly);

            // Initialize the patch
            RpTextEntityPlayerShapeRendererPatch.Initialize(api);
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony?.UnpatchAll(harmony.Id);
        }
    }
} 