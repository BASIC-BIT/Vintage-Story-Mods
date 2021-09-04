using Vintagestory.API.Common;

namespace makersmark
{
    public class Universal : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }
        
        public override void Start(ICoreAPI coreApi)
        {
            base.Start(coreApi);
            //
            // ((ICoreAPICommon) coreApi).RegisterItemClass("ItemWand", typeof (ItemWand));
            // ((ICoreAPICommon) coreApi).RegisterEntity("EntityProjectileSpell", typeof (EntityProjectileSpell));
        }
    }
}