using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class FlywheelPowerModSystem : ModSystem
{
    internal static readonly string[] FullWheelMaterials = { "wood", "iron", "meteoriciron", "steel" };
    internal static readonly string[] CompactWheelMaterials = { "wood", "stone", "iron", "meteoriciron", "steel" };
    internal static readonly string[] HubMaterials = { "iron", "meteoriciron", "steel" };
    internal static readonly string[] ReleasedRendererCodes = BuildReleasedRendererCodes();

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockFlywheel", typeof(BlockFlywheel));
        api.RegisterBlockClass("BlockCompactFlywheel", typeof(BlockCompactFlywheel));
        api.RegisterBlockClass("BlockFlywheelStand", typeof(BlockFlywheelStand));
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

    internal static bool IsReleasedMaterialCombination(string wheelMaterial, string hubMaterial)
    {
        int wheelTier = wheelMaterial switch
        {
            "wood" or "stone" => 0,
            "iron" or "meteoriciron" => 1,
            "steel" => 2,
            _ => int.MaxValue
        };
        int hubTier = hubMaterial switch
        {
            "iron" or "meteoriciron" => 1,
            "steel" => 2,
            _ => int.MinValue
        };
        return wheelTier <= hubTier;
    }

    internal static string RendererCode(bool compact, string wheelMaterial, string hubMaterial)
    {
        return $"flywheelpower-{(compact ? "compact" : "full")}-{wheelMaterial}-{hubMaterial}hub";
    }

    private static string[] BuildReleasedRendererCodes()
    {
        return FullWheelMaterials
            .SelectMany(wheel => HubMaterials
                .Where(hub => IsReleasedMaterialCombination(wheel, hub))
                .Select(hub => RendererCode(compact: false, wheel, hub)))
            .Concat(CompactWheelMaterials
                .SelectMany(wheel => HubMaterials
                    .Where(hub => IsReleasedMaterialCombination(wheel, hub))
                    .Select(hub => RendererCode(compact: true, wheel, hub))))
            .ToArray();
    }
}
