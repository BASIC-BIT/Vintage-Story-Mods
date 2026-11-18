using FlywheelPower;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace FlywheelPower.Tests;

public sealed class FlywheelPartSerializationTests
{
    [Fact]
    public void PrincipalRoundTripPreservesNegativeCoordinatesAndDimension()
    {
        BEFlywheelPart source = new()
        {
            Principal = new BlockPos(-12, 34, -56, 7)
        };
        BlockPos sourcePartPosition = new(-11, 35, -56, 7);
        TreeAttribute tree = new();

        source.WritePrincipal(tree, sourcePartPosition);
        BEFlywheelPart restored = new();
        restored.ReadPrincipal(tree, sourcePartPosition);

        Assert.NotNull(restored.Principal);
        Assert.Equal(-12, restored.Principal.X);
        Assert.Equal(34, restored.Principal.Y);
        Assert.Equal(-56, restored.Principal.Z);
        Assert.Equal(7, restored.Principal.dimension);
    }

    [Fact]
    public void RelativePrincipalLinkMovesWithSchematicPlacement()
    {
        BlockPos sourcePartPosition = new(-11, 35, -56, 7);
        BEFlywheelPart source = new()
        {
            Principal = new BlockPos(-12, 34, -56, 7)
        };
        TreeAttribute tree = new();
        source.WritePrincipal(tree, sourcePartPosition);

        BEFlywheelPart restored = new();
        restored.ReadPrincipal(tree, new BlockPos(101, 65, 202, 3));

        Assert.Equal(new BlockPos(100, 64, 202, 3), restored.Principal);
    }

    [Fact]
    public void PrincipalSerializationOmitsUnpublishedAbsolutePrototypeFields()
    {
        BEFlywheelPart source = new()
        {
            Principal = new BlockPos(-12, 34, -56, 7)
        };
        TreeAttribute tree = new();
        source.WritePrincipal(tree, new BlockPos(-11, 35, -56, 7));

        Assert.False(tree.HasAttribute("cx"));
        Assert.False(tree.HasAttribute("cy"));
        Assert.False(tree.HasAttribute("cz"));
        Assert.False(tree.HasAttribute("cd"));
    }

    [Theory]
    [InlineData(90, -3, 2)]
    [InlineData(180, -2, -3)]
    [InlineData(270, 3, -2)]
    public void RelativePrincipalLinkRotatesWithSchematic(int degrees, int expectedX, int expectedZ)
    {
        TreeAttribute tree = RelativePrincipalTree(2, -1, 3);

        BEFlywheelPart.TransformRelativePrincipal(tree, degrees, null);

        Assert.Equal(expectedX, tree.GetInt("rx"));
        Assert.Equal(-1, tree.GetInt("ry"));
        Assert.Equal(expectedZ, tree.GetInt("rz"));
    }

    [Theory]
    [InlineData(EnumAxis.X, -2, -1, 3)]
    [InlineData(EnumAxis.Y, 2, 1, 3)]
    [InlineData(EnumAxis.Z, 2, -1, -3)]
    public void RelativePrincipalLinkFlipsBeforeRotation(
        EnumAxis flipAxis,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        TreeAttribute tree = RelativePrincipalTree(2, -1, 3);

        BEFlywheelPart.TransformRelativePrincipal(tree, 0, flipAxis);

        Assert.Equal(expectedX, tree.GetInt("rx"));
        Assert.Equal(expectedY, tree.GetInt("ry"));
        Assert.Equal(expectedZ, tree.GetInt("rz"));
    }

    [Fact]
    public void RelativePrincipalLinkAppliesFlipBeforeRotation()
    {
        TreeAttribute tree = RelativePrincipalTree(2, -1, 3);

        BEFlywheelPart.TransformRelativePrincipal(tree, 90, EnumAxis.X);

        Assert.Equal(-3, tree.GetInt("rx"));
        Assert.Equal(-1, tree.GetInt("ry"));
        Assert.Equal(-2, tree.GetInt("rz"));
    }

    private static TreeAttribute RelativePrincipalTree(int x, int y, int z)
    {
        TreeAttribute tree = new();
        tree.SetBool("pr", true);
        tree.SetInt("rx", x);
        tree.SetInt("ry", y);
        tree.SetInt("rz", z);
        return tree;
    }
}
