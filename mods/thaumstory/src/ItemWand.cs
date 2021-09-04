using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thaumstory
{
    public class ItemWand : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
            EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation("thaumstory:projectile-spell"));
            EntityProjectile entity = (EntityProjectile) byEntity.World.ClassRegistry.CreateEntity(entityType);
            entity.FiredBy = byEntity;
            entity.Damage = 0;
            var SPEEEEED = 0.4f;
            var SHOT_HEIGHT = 0.87f;
            Vec3d vec3d = byEntity.ServerPos.XYZ.Add(0.0, byEntity.LocalEyePos.Y, 0.0);
            Vec3d pos = (vec3d.AheadCopy(0.5, (double)byEntity.SidedPos.Pitch, (double)byEntity.SidedPos.Yaw) - vec3d) *
                        SPEEEEED;
            entity.ServerPos.SetPos(byEntity.SidedPos.AheadCopy(0.1).XYZ.Add(0.0, byEntity.LocalEyePos.Y * SHOT_HEIGHT, 0.0));
            entity.ServerPos.Motion.Set(pos);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            entity.SetRotation();
            byEntity.World.SpawnEntity(entity);
            
            handling = EnumHandHandling.PreventDefaultAction;
        }
    }
}