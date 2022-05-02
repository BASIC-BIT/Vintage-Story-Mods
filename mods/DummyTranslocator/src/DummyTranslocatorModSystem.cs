using DummyTranslocator.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DummyTranslocator
{
    public class DummyTranslocatorModSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("dummytranslocator", typeof(BlockDummyTranslocator));
        }

    }
}