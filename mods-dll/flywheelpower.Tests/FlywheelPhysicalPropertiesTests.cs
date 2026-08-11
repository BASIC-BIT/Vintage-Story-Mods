namespace FlywheelPower.Tests;

public sealed class FlywheelPhysicalPropertiesTests
{
    [Fact]
    public void FullIronConstructionDefinesTheReferenceInertia()
    {
        FlywheelPhysicalProfile profile = FlywheelPhysicalProperties.ForVariant(
            compact: false,
            wheelMaterial: "iron",
            hubMaterial: "iron");

        Assert.Equal(8f, profile.EffectiveInertia, 3);
    }

    [Fact]
    public void MetalWheelMassCountsTwoIngotsPerPlate()
    {
        FlywheelPhysicalProfile fullIron = FlywheelPhysicalProperties.ForVariant(false, "iron", "iron");
        FlywheelPhysicalProfile compactIron = FlywheelPhysicalProperties.ForVariant(true, "iron", "iron");

        Assert.Equal(182.4f, fullIron.RotatingMassKg, 2);
        Assert.Equal(73.83f, compactIron.RotatingMassKg, 2);
    }

    [Fact]
    public void ReleasedFullSizeConstructionsHaveMaterialDependentProfiles()
    {
        FlywheelPhysicalProfile wood = FlywheelPhysicalProperties.ForVariant(false, "wood", "iron");
        FlywheelPhysicalProfile iron = FlywheelPhysicalProperties.ForVariant(false, "iron", "iron");
        FlywheelPhysicalProfile meteoric = FlywheelPhysicalProperties.ForVariant(false, "meteoriciron", "meteoriciron");
        FlywheelPhysicalProfile steel = FlywheelPhysicalProperties.ForVariant(false, "steel", "steel");

        Assert.True(wood.RotatingMassKg < iron.RotatingMassKg);
        Assert.True(wood.EffectiveInertia < iron.EffectiveInertia);
        Assert.True(meteoric.RotatingMassKg < steel.RotatingMassKg);
        Assert.True(meteoric.EffectiveInertia < steel.EffectiveInertia);
        Assert.NotEqual(iron.RotatingMassKg, meteoric.RotatingMassKg);
        Assert.InRange(wood.RotatingMassKg, 75f, 85f);
        Assert.InRange(steel.RotatingMassKg, 175f, 185f);
    }

    [Fact]
    public void CompactConstructionsPreserveInertiaOrderingAndStoneFillsTheMiddleTier()
    {
        FlywheelPhysicalProfile fullIron = FlywheelPhysicalProperties.ForVariant(false, "iron", "iron");
        FlywheelPhysicalProfile compactWood = FlywheelPhysicalProperties.ForVariant(true, "wood", "iron");
        FlywheelPhysicalProfile compactStone = FlywheelPhysicalProperties.ForVariant(true, "stone", "iron");
        FlywheelPhysicalProfile compactIron = FlywheelPhysicalProperties.ForVariant(true, "iron", "iron");

        Assert.True(compactWood.RotatingMassKg < compactStone.RotatingMassKg);
        Assert.True(compactStone.RotatingMassKg < compactIron.RotatingMassKg);
        Assert.True(compactWood.EffectiveInertia < compactStone.EffectiveInertia);
        Assert.True(compactStone.EffectiveInertia < compactIron.EffectiveInertia);
        Assert.True(compactIron.EffectiveInertia < fullIron.EffectiveInertia);
        Assert.InRange(compactWood.RotatingMassKg, 18f, 23f);
        Assert.InRange(compactStone.RotatingMassKg, 28f, 33f);
        Assert.InRange(compactIron.RotatingMassKg, 70f, 80f);
        Assert.True(compactIron.RotatingMassKg < fullIron.RotatingMassKg);
    }

    [Fact]
    public void IndependentlySelectedHubMaterialChangesPhysicalProfile()
    {
        FlywheelPhysicalProfile ironHub = FlywheelPhysicalProperties.ForVariant(false, "iron", "iron");
        FlywheelPhysicalProfile steelHub = FlywheelPhysicalProperties.ForVariant(false, "iron", "steel");

        Assert.NotEqual(steelHub.RotatingMassKg, ironHub.RotatingMassKg);
        Assert.NotEqual(steelHub.EffectiveInertia, ironHub.EffectiveInertia);
    }
}
