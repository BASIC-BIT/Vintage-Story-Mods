using System;
using Vintagestory.API.Common;

namespace FlywheelPower;

internal readonly record struct FlywheelPhysicalProfile(float RotatingMassKg, float EffectiveInertia);

internal static class FlywheelPhysicalProperties
{
    private const float WoodDensity = 700f;
    private const float StoneDensity = 2600f;
    private const float CopperDensity = 8960f;
    private const float TinBronzeDensity = 7600f;
    private const float BismuthBronzeDensity = 7900f;
    private const float BlackBronzeDensity = 9000f;
    private const float IronDensity = 7870f;
    private const float MeteoricIronDensity = 7800f;
    private const float SteelDensity = 7820f;
    private const float IngotEquivalentVolumeM3 = 0.001f;
    private const float WoodPlankMassKg = 2.5f;
    private const float StoneBlankPieceMassKg = 5f;
    private const float FullAxleMassKg = 5f;
    private const float CompactAxleMassKg = 3f;
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
            "copper" => CopperDensity,
            "tinbronze" => TinBronzeDensity,
            "bismuthbronze" => BismuthBronzeDensity,
            "blackbronze" => BlackBronzeDensity,
            "meteoriciron" => MeteoricIronDensity,
            "steel" => SteelDensity,
            _ => IronDensity
        };
    }

    private static FlywheelPhysicalProfile Calculate(PhysicalSpec spec, float wheelDensity, float hubDensity, float referenceInertia)
    {
        Component wheel = spec.IsCompact
            ? Annulus(wheelDensity, spec.WheelInnerRadius, spec.WheelOuterRadius, spec.WheelThickness)
            : FullSizeWheel(wheelDensity, spec);
        Component hub = Annulus(hubDensity, spec.BearingOuterRadius, spec.HubOuterRadius, spec.HubThickness);
        Component bearing = Annulus(IronDensity, spec.ShaftClearanceRadius, spec.BearingOuterRadius, spec.BearingThickness);
        Component plates = Annulus(hubDensity, spec.ShaftClearanceRadius, spec.PlateOuterRadius, spec.PlateThickness * 2f);
        Component axle = SolidCylinder(WoodDensity, spec.AxleRadius, spec.AxleLength);

        float mass = EstimateRecipeMass(spec.IsCompact, wheelDensity, hubDensity);
        float polarInertia = wheel.PolarInertia + hub.PolarInertia + bearing.PolarInertia + plates.PolarInertia + axle.PolarInertia;
        float referencePolarInertia = FullIronReferencePolarInertia();
        float effectiveInertia = Math.Max(0.01f, referenceInertia * polarInertia / referencePolarInertia);
        return new FlywheelPhysicalProfile(mass, effectiveInertia);
    }

    private static float EstimateRecipeMass(bool compact, float wheelDensity, float hubDensity)
    {
        int rimPieces = compact ? 4 : 8;
        int hubIngotEquivalents = compact ? 2 : 8;
        int webPlanks = compact ? 4 : 8;
        float rimPieceMass = wheelDensity switch
        {
            WoodDensity => WoodPlankMassKg,
            StoneDensity => StoneBlankPieceMassKg,
            _ => wheelDensity * IngotEquivalentVolumeM3
        };

        return rimPieces * rimPieceMass
            + hubIngotEquivalents * hubDensity * IngotEquivalentVolumeM3
            + webPlanks * WoodPlankMassKg
            + (compact ? CompactAxleMassKg : FullAxleMassKg);
    }

    private static float FullIronReferencePolarInertia()
    {
        PhysicalSpec spec = FullSpec();
        return FullSizeWheel(IronDensity, spec).PolarInertia
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

    private static Component FullSizeWheel(float tyreDensity, PhysicalSpec spec)
    {
        Component tyre = Annulus(
            tyreDensity,
            FlywheelModelDimensions.TyreInnerRadius,
            spec.WheelOuterRadius,
            spec.WheelThickness);
        Component felloe = Annulus(
            WoodDensity,
            FlywheelModelDimensions.FelloeInnerRadius,
            FlywheelModelDimensions.FelloeOuterRadius,
            spec.WheelThickness);

        float spokeInner = spec.HubOuterRadius * 0.92f;
        float spokeOuter = FlywheelModelDimensions.FelloeInnerRadius + 0.02f;
        float spokeWidth = FlywheelModelDimensions.SpokeHalfWidth * 2f;
        float spokeLength = spokeOuter - spokeInner;
        float spokeMass = WoodDensity * spokeLength * spokeWidth * spec.WheelThickness;
        float spokePolar = spokeMass * (
            (spokeInner * spokeInner + spokeInner * spokeOuter + spokeOuter * spokeOuter) / 3f
            + spokeWidth * spokeWidth / 12f);
        Component spokes = new(
            spokeMass * FlywheelModelDimensions.SpokeCount,
            spokePolar * FlywheelModelDimensions.SpokeCount);

        return new Component(
            tyre.MassKg + felloe.MassKg + spokes.MassKg,
            tyre.PolarInertia + felloe.PolarInertia + spokes.PolarInertia);
    }

    private static bool IsCompact(Block block)
    {
        return block?.Code?.Path?.StartsWith("compactflywheel", StringComparison.Ordinal) == true;
    }

    private static string DefaultCompactHub(string wheelMaterial)
    {
        return wheelMaterial is "copper"
            or "tinbronze"
            or "bismuthbronze"
            or "blackbronze"
            or "iron"
            or "meteoriciron"
            or "steel"
            ? wheelMaterial
            : "copper";
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
            FullAxleLength,
            IsCompact: false);
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
            CompactAxleLength,
            IsCompact: true);
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
        float AxleLength,
        bool IsCompact);
}
