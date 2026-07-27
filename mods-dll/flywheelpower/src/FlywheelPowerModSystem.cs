using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class FlywheelPowerModSystem : ModSystem
{
    internal static readonly string[] ReleasedRendererCodes =
    {
        "flywheelpower-full-wood-ironhub",
        "flywheelpower-full-iron-ironhub",
        "flywheelpower-full-meteoriciron-ironhub",
        "flywheelpower-full-steel-ironhub",
        "flywheelpower-compact-wood",
        "flywheelpower-compact-stone",
        "flywheelpower-compact-iron",
        "flywheelpower-compact-meteoriciron",
        "flywheelpower-compact-steel"
    };

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
        foreach (string rendererCode in ReleasedRendererCodes)
        {
            MechNetworkRenderer.RendererByCode[rendererCode] = typeof(FlywheelMechBlockRenderer);
        }
    }
}
