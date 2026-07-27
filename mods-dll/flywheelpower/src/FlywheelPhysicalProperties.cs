using System;
using Vintagestory.API.Common;

namespace FlywheelPower;

internal readonly record struct FlywheelPhysicalProfile(float RotatingMassKg, float EffectiveInertia);

internal static class FlywheelPhysicalProperties
{
    private const float WoodDensity = 700f;
    private const float StoneDensity = 2600f;
    private const float BronzeDensity = 8800f;
    private const float IronDensity = 7870f;
    private const float MeteoricIronDensity = 7800f;
    private const float SteelDensity = 7820f;
    private const float FullAxleLength = 1.5f;
    private const float CompactAxleLength = 1.16f;

    internal static FlywheelPhysicalProfile ForBlock(Block block, float referenceInertia = 8f)
    {
        bool compact = IsCompact(block);
        string wheelMaterial = block?.Variant?["material"] ?? "iron";
        string hubMaterial = block?.Variant?["hub"] ?? (compact ? DefaultCompactHub(wheelMaterial) : "iron");
        return ForVariant(compact, wheelMaterial, hubMaterial, referenceInertia);
    }

    internal static FlywheelPhysicalProfile ForVariant(
        bool compact,
        string wheelMaterial,
        string hubMaterial,
        float referenceInertia = 8f)
    {
        PhysicalSpec spec = compact ? CompactSpec() : FullSpec();
        return Calculate(spec, GetMaterialDensity(wheelMaterial), GetMaterialDensity(hubMaterial), referenceInertia);
    }

    internal static float GetMaterialDensity(string material)
    {
        return material switch
        {
            "wood" => WoodDensity,
            "stone" => StoneDensity,
            "bronze" => BronzeDensity,
            "meteoriciron" => MeteoricIronDensity,
            "steel" => SteelDensity,
            _ => IronDensity
        };
    }

    private static FlywheelPhysicalProfile Calculate(PhysicalSpec spec, float wheelDensity, float hubDensity, float referenceInertia)
    {
        Component wheel = Annulus(wheelDensity, spec.WheelInnerRadius, spec.WheelOuterRadius, spec.WheelThickness);
        Component hub = Annulus(hubDensity, spec.BearingOuterRadius, spec.HubOuterRadius, spec.HubThickness);
        Component bearing = Annulus(IronDensity, spec.ShaftClearanceRadius, spec.BearingOuterRadius, spec.BearingThickness);
        Component plates = Annulus(hubDensity, spec.ShaftClearanceRadius, spec.PlateOuterRadius, spec.PlateThickness * 2f);
        Component axle = SolidCylinder(WoodDensity, spec.AxleRadius, spec.AxleLength);

        float mass = wheel.MassKg + hub.MassKg + bearing.MassKg + plates.MassKg + axle.MassKg;
        float polarInertia = wheel.PolarInertia + hub.PolarInertia + bearing.PolarInertia + plates.PolarInertia + axle.PolarInertia;
        float referencePolarInertia = FullIronReferencePolarInertia();
        float effectiveInertia = Math.Max(0.01f, referenceInertia * polarInertia / referencePolarInertia);
        return new FlywheelPhysicalProfile(mass, effectiveInertia);
    }

    private static float FullIronReferencePolarInertia()
    {
        PhysicalSpec spec = FullSpec();
        return Annulus(IronDensity, spec.WheelInnerRadius, spec.WheelOuterRadius, spec.WheelThickness).PolarInertia
            + Annulus(IronDensity, spec.BearingOuterRadius, spec.HubOuterRadius, spec.HubThickness).PolarInertia
            + Annulus(IronDensity, spec.ShaftClearanceRadius, spec.BearingOuterRadius, spec.BearingThickness).PolarInertia
            + Annulus(IronDensity, spec.ShaftClearanceRadius, spec.PlateOuterRadius, spec.PlateThickness * 2f).PolarInertia
            + SolidCylinder(WoodDensity, spec.AxleRadius, spec.AxleLength).PolarInertia;
    }

    private static Component Annulus(float density, float innerRadius, float outerRadius, float thickness)
    {
        float mass = density * MathF.PI * (outerRadius * outerRadius - innerRadius * innerRadius) * thickness;
        float polarInertia = 0.5f * mass * (outerRadius * outerRadius + innerRadius * innerRadius);
        return new Component(mass, polarInertia);
    }

    private static Component SolidCylinder(float density, float radius, float length)
    {
        float mass = density * MathF.PI * radius * radius * length;
        return new Component(mass, 0.5f * mass * radius * radius);
    }

    private static bool IsCompact(Block block)
    {
        return block?.Code?.Path?.StartsWith("compactflywheel", StringComparison.Ordinal) == true;
    }

    private static string DefaultCompactHub(string wheelMaterial)
    {
        return wheelMaterial is "iron" or "meteoriciron" or "steel" ? wheelMaterial : "iron";
    }

    private static PhysicalSpec FullSpec()
    {
        return new PhysicalSpec(
            FlywheelModelDimensions.CoupledInnerRadius,
            FlywheelModelDimensions.WheelOuterRadius,
            FlywheelModelDimensions.WheelHalfThickness * 2f,
            FlywheelModelDimensions.HubOuterRadius,
            FlywheelModelDimensions.HubHalfThickness * 2f,
            FlywheelModelDimensions.BearingOuterRadius,
            FlywheelModelDimensions.BearingHalfThickness * 2f,
            FlywheelModelDimensions.ShaftClearanceRadius,
            FlywheelModelDimensions.CouplingPlateOuterRadius,
            FlywheelModelDimensions.CouplingPlateThickness,
            FlywheelModelDimensions.AxleRadius,
            FullAxleLength);
    }

    private static PhysicalSpec CompactSpec()
    {
        return new PhysicalSpec(
            FlywheelModelDimensions.CompactCoupledInnerRadius,
            FlywheelModelDimensions.CompactWheelOuterRadius,
            FlywheelModelDimensions.CompactWheelHalfThickness * 2f,
            FlywheelModelDimensions.CompactHubOuterRadius,
            FlywheelModelDimensions.CompactHubHalfThickness * 2f,
            FlywheelModelDimensions.CompactBearingOuterRadius,
            FlywheelModelDimensions.CompactBearingHalfThickness * 2f,
            FlywheelModelDimensions.CompactShaftClearanceRadius,
            FlywheelModelDimensions.CompactCouplingPlateOuterRadius,
            FlywheelModelDimensions.CompactCouplingPlateThickness,
            FlywheelModelDimensions.CompactAxleRadius,
            CompactAxleLength);
    }

    private readonly record struct Component(float MassKg, float PolarInertia);

    private readonly record struct PhysicalSpec(
        float WheelInnerRadius,
        float WheelOuterRadius,
        float WheelThickness,
        float HubOuterRadius,
        float HubThickness,
        float BearingOuterRadius,
        float BearingThickness,
        float ShaftClearanceRadius,
        float PlateOuterRadius,
        float PlateThickness,
        float AxleRadius,
        float AxleLength);
}
