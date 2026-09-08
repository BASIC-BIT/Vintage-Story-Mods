using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

// Vanilla padlocks require reinforcement and follow its group grants. Only scene markers
// use their immutable creator and their own persisted lock; all other blocks stay vanilla.
[HarmonyPatch(typeof(ItemPadlock), nameof(ItemPadlock.OnHeldInteractStart))]
internal static class SceneDescriptionPadlockPatch
{
    [HarmonyPrefix]
    internal static bool BeforeInteract(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel == null || byEntity?.World?.BlockAccessor.GetBlock(blockSel.Position) is not SceneDescriptionBlock)
        {
            return true;
        }

        handling = EnumHandHandling.PreventDefault;
        if (!CanApplyOnServer(byEntity, firstEvent))
        {
            return false;
        }

        var player = (byEntity as EntityPlayer)?.Player;
        if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not SceneDescriptionBlockEntity marker || !marker.TryLock(player, slot))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-cannot-lock", Lang.Get("thebasics:scene-description-cannot-lock"));
        }

        return false;
    }

    private static bool CanApplyOnServer(EntityAgent entity, bool firstEvent) =>
        entity.World.Side == EnumAppSide.Server && firstEvent && entity.Controls.ShiftKey;
}
