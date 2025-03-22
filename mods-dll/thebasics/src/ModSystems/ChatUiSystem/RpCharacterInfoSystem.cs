using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace thebasics.ModSystems.ChatUiSystem
{
    public class RpCharacterInfoSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterEntityBehaviorClass("rpcharacterinfo", typeof(RpCharacterInfoBehavior));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.Event.OnEntitySpawn += OnEntitySpawn;
        }

        private void OnEntitySpawn(Entity entity)
        {
            if (entity is EntityPlayer)
            {
                entity.AddBehavior(new RpCharacterInfoBehavior(entity));
            }
        }
    }
} 