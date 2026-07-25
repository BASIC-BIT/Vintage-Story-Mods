using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class FlywheelPowerModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockFlywheel", typeof(BlockFlywheel));
        api.RegisterBlockClass("BlockCompactFlywheel", typeof(BlockCompactFlywheel));
        api.RegisterBlockClass("BlockFlywheelPart", typeof(BlockFlywheelPart));
        api.RegisterBlockEntityClass("FlywheelPart", typeof(BEFlywheelPart));
        api.RegisterBlockEntityBehaviorClass("MPFlywheel", typeof(BEBehaviorMPFlywheel));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        MechNetworkRenderer.RendererByCode["flywheelpower"] = typeof(FlywheelMechBlockRenderer);
    }
}
