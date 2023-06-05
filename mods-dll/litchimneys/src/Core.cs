using Vintagestory.API.Common;

namespace litchimneys
{
    class LitChimneysCoreSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockEntityBehaviorClass("litchimney", typeof(LitChimneyBlockEntityBehavior));
        }
    }
}
