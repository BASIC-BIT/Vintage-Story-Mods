using FluentAssertions;
using NSubstitute;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.Utilities;

public class BlockOnlyRaycastTests
{
    private static readonly Block Air = new()
    {
        BlockId = 0,
        CollisionBoxes = [],
        SelectionBoxes = []
    };

    [Fact]
    public void PreservesPartialSelectionGeometryWithoutEnumeratingEntities()
    {
        var upperBox = new CuboidfWithId(0, 0.5f, 0, 1, 1, 1) { Id = "upper" };
        var world = new TrackingIntersectionSupplier();
        world.PutBlock(
            x: 2,
            internalY: 0,
            z: 0,
            new Block { BlockId = 1, BlockMaterial = EnumBlockMaterial.Stone },
            new Cuboidf(0, 0, 0, 1, 0.5f, 1),
            upperBox);

        var selection = VisibilityUtils.RayTraceBlocksForSelection(
            world,
            new Vec3d(0.5, 0.75, 0.5),
            new Vec3d(4.5, 0.75, 0.5),
            (_, _) => true);

        selection.Should().NotBeNull();
        selection!.Position.X.Should().Be(2);
        selection.Position.InternalY.Should().Be(0);
        selection.Position.Z.Should().Be(0);
        selection.Face.Should().Be(BlockFacing.WEST);
        selection.SelectionBoxIndex.Should().Be(1);
        selection.SelectionBoxId.Should().Be("upper");
        selection.HitPosition.X.Should().BeApproximately(0, 0.000001);
        selection.HitPosition.Y.Should().BeApproximately(0.75, 0.000001);
        world.EntitySearchCount.Should().Be(0);
    }

    [Fact]
    public void PreservesBlockFilterAndContinuesToTheNextSelectionBox()
    {
        var world = new TrackingIntersectionSupplier();
        world.PutBlock(2, 0, 0,
            new Block
            {
                BlockId = 1,
                BlockMaterial = EnumBlockMaterial.Glass,
                RenderPass = EnumChunkRenderPass.Transparent
            },
            new Cuboidf(0, 0, 0, 1, 1, 1));
        world.PutBlock(3, 0, 0,
            new Block { BlockId = 2, BlockMaterial = EnumBlockMaterial.Stone },
            new Cuboidf(0, 0, 0, 1, 1, 1));

        var selection = VisibilityUtils.RayTraceBlocksForSelection(
            world,
            new Vec3d(0.5, 0.5, 0.5),
            new Vec3d(4.5, 0.5, 0.5),
            VisibilityUtils.SightBlockFilter);

        selection.Should().NotBeNull($"block traversal visited {string.Join(", ", world.BlockLookups)}");
        selection!.Position.X.Should().Be(3);
        selection.Block.Id.Should().Be(2);
        world.EntitySearchCount.Should().Be(0);
    }

    [Fact]
    public void PreservesMissesAgainstPartialSelectionBoxes()
    {
        var world = new TrackingIntersectionSupplier();
        world.PutBlock(
            2,
            0,
            0,
            new Block { BlockId = 1, BlockMaterial = EnumBlockMaterial.Stone },
            new Cuboidf(0, 0, 0, 1, 0.5f, 1));

        var selection = VisibilityUtils.RayTraceBlocksForSelection(
            world,
            new Vec3d(0.5, 0.75, 0.5),
            new Vec3d(4.5, 0.75, 0.5),
            (_, _) => true);

        selection.Should().BeNull();
        world.EntitySearchCount.Should().Be(0);
    }

    [Fact]
    public void IncludesASelectionBoxHitExactlyAtTheRayEndpoint()
    {
        var world = new TrackingIntersectionSupplier();
        world.PutBlock(
            2,
            0,
            0,
            new Block { BlockId = 1, BlockMaterial = EnumBlockMaterial.Stone },
            new Cuboidf(0, 0, 0, 1, 1, 1));

        var selection = VisibilityUtils.RayTraceBlocksForSelection(
            world,
            new Vec3d(0.5, 0.5, 0.5),
            new Vec3d(2, 0.5, 0.5),
            (_, _) => true);

        selection.Should().NotBeNull();
        selection!.Position.X.Should().Be(2);
        selection.HitPosition.X.Should().BeApproximately(0, 0.000001);
        world.EntitySearchCount.Should().Be(0);
    }

    [Theory]
    [InlineData(-2, 0, -3, 1)]
    [InlineData(2, 0, 0.5, 4.5)]
    [InlineData(2, 32778, 0.5, 4.5)]
    public void PreservesNegativeAndDimensionEncodedCoordinates(
        int blockX,
        int internalY,
        double fromX,
        double toX)
    {
        var world = new TrackingIntersectionSupplier();
        world.PutBlock(
            blockX,
            internalY,
            0,
            new Block { BlockId = 1, BlockMaterial = EnumBlockMaterial.Stone },
            new Cuboidf(0, 0, 0, 1, 1, 1));

        var selection = VisibilityUtils.RayTraceBlocksForSelection(
            world,
            new Vec3d(fromX, internalY + 0.5, 0.5),
            new Vec3d(toX, internalY + 0.5, 0.5),
            (_, _) => true);

        selection.Should().NotBeNull($"block traversal visited {string.Join(", ", world.BlockLookups)}");
        selection!.Position.X.Should().Be(blockX);
        selection.Position.InternalY.Should().Be(internalY);
        world.EntitySearchCount.Should().Be(0);
    }

    private sealed class TrackingIntersectionSupplier : IWorldIntersectionSupplier
    {
        private readonly Dictionary<(int X, int InternalY, int Z), (Block Block, Cuboidf[] Boxes)> _blocks = new();

        public TrackingIntersectionSupplier()
        {
            blockAccessor = Substitute.For<IBlockAccessor>();
            blockAccessor.GetBlock(Arg.Any<BlockPos>(), Arg.Any<int>())
                .Returns(call => GetBlock(call.ArgAt<BlockPos>(0)));
            blockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns((IWorldChunk)null!);
        }

        public int EntitySearchCount { get; private set; }

        public List<string> BlockLookups { get; } = [];

        public Vec3i MapSize { get; } = new(1024, 1024, 1024);

        public IBlockAccessor blockAccessor { get; }

        public void PutBlock(int x, int internalY, int z, Block block, params Cuboidf[] boxes)
        {
            block.CollisionBoxes = boxes;
            block.SelectionBoxes = boxes;
            _blocks[(x, internalY, z)] = (block, boxes);
        }

        public Block GetBlock(BlockPos pos)
        {
            BlockLookups.Add($"({pos.X},{pos.InternalY},{pos.Z})");
            return _blocks.TryGetValue((pos.X, pos.InternalY, pos.Z), out var entry)
                ? entry.Block
                : Air;
        }

        public Cuboidf[] GetBlockIntersectionBoxes(BlockPos pos)
        {
            return _blocks.TryGetValue((pos.X, pos.InternalY, pos.Z), out var entry)
                ? entry.Boxes
                : [];
        }

        public Entity[] GetEntitiesAround(
            Vec3d position,
            float horRange,
            float vertRange,
            ActionConsumable<Entity> matches = null!)
        {
            EntitySearchCount++;
            throw new InvalidOperationException("A block-only raycast must not enumerate entities.");
        }

        public bool IsValidPos(BlockPos pos) => true;
    }
}
