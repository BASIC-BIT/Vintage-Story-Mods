using FluentAssertions;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.Utilities;

/// <summary>
/// The three block filters encode a deliberate disagreement about what stops what. Sight and sound
/// are not the same physics, and the mod models both.
///
/// These are unit-level because the bug they guard against is invisible in game until someone
/// stands in exactly the right hedge: a signed message arrived while its speech bubble did not,
/// because delivery and rendering asked different filters the same question.
/// </summary>
public class VisibilityFilterTests
{
    private static readonly BlockPos AnyPos = new(0, 0, 0, 0);

    private static Block Foliage(EnumBlockMaterial material) => new()
    {
        // Id 0 is air; anything else is a real block. Foliage declares no render pass, so it
        // defaults to Opaque — which is exactly why it needs the material check.
        BlockId = 1,
        BlockMaterial = material,
        CollisionBoxes = [new Cuboidf(0, 0, 0, 1, 1, 1)]
    };

    private static Block Glass() => new()
    {
        BlockId = 1,
        BlockMaterial = EnumBlockMaterial.Glass,
        RenderPass = EnumChunkRenderPass.Transparent,
        CollisionBoxes = [new Cuboidf(0, 0, 0, 1, 1, 1)]
    };

    private static Block Stone() => new()
    {
        BlockId = 1,
        BlockMaterial = EnumBlockMaterial.Stone,
        CollisionBoxes = [new Cuboidf(0, 0, 0, 1, 1, 1)]
    };

    private static Block Water() => new()
    {
        BlockId = 1,
        BlockMaterial = EnumBlockMaterial.Water,
        RenderPass = EnumChunkRenderPass.Liquid,
        CollisionBoxes = []
    };

    public class SightAndSoundDisagreeOnPurpose
    {
        [Theory]
        [InlineData(EnumBlockMaterial.Leaves)]
        [InlineData(EnumBlockMaterial.Plant)]
        public void FoliageBlocksNeitherSightNorSound(EnumBlockMaterial material)
        {
            var block = Foliage(material);

            VisibilityUtils.SightBlockFilter(AnyPos, block).Should().BeFalse();
            VisibilityUtils.SoundBlockFilter(AnyPos, block).Should().BeFalse();
        }

        [Fact]
        public void GlassBlocksSoundButNotSight()
        {
            var glass = Glass();

            VisibilityUtils.SightBlockFilter(AnyPos, glass).Should().BeFalse();
            VisibilityUtils.SoundBlockFilter(AnyPos, glass).Should().BeTrue();
        }

        [Fact]
        public void StoneBlocksBoth()
        {
            var stone = Stone();

            VisibilityUtils.SightBlockFilter(AnyPos, stone).Should().BeTrue();
            VisibilityUtils.SoundBlockFilter(AnyPos, stone).Should().BeTrue();
        }

        [Fact]
        public void WaterBlocksSoundDespiteHavingNoCollisionBox()
        {
            var water = Water();

            VisibilityUtils.SoundBlockFilter(AnyPos, water).Should().BeTrue();
            VisibilityUtils.SightBlockFilter(AnyPos, water).Should().BeFalse();
        }
    }

    public class StrictSightIsForCloseInspectionOnly
    {
        [Theory]
        [InlineData(EnumBlockMaterial.Leaves)]
        [InlineData(EnumBlockMaterial.Plant)]
        public void FoliageBlocksStrictSightButNotGeneralSight(EnumBlockMaterial material)
        {
            // The whole reason two sight filters exist. General sight backs delivery, speech and
            // placed bubbles, nametags and the typing indicator; strict sight backs one thing only,
            // reading another player's character sheet.
            var block = Foliage(material);

            VisibilityUtils.StrictSightBlockFilter(AnyPos, block).Should().BeTrue();
            VisibilityUtils.SightBlockFilter(AnyPos, block).Should().BeFalse();
        }

        [Fact]
        public void TheTwoSightFiltersAgreeOnEverythingElse()
        {
            foreach (var block in new[] { Glass(), Stone(), Water() })
            {
                VisibilityUtils.SightBlockFilter(AnyPos, block)
                    .Should().Be(VisibilityUtils.StrictSightBlockFilter(AnyPos, block),
                        "foliage is the only thing the two sight filters should disagree about");
            }
        }
    }

    public class UnknownBlocksFailClosed
    {
        [Fact]
        public void AnUnrecognisedOpaqueBlockStopsSightAndSound()
        {
            // Default-deny: a block from another mod must not silently leak chat through terrain.
            var unknown = new Block
            {
                BlockId = 1,
                BlockMaterial = EnumBlockMaterial.Ceramic,
                CollisionBoxes = [new Cuboidf(0, 0, 0, 1, 1, 1)]
            };

            VisibilityUtils.SightBlockFilter(AnyPos, unknown).Should().BeTrue();
            VisibilityUtils.StrictSightBlockFilter(AnyPos, unknown).Should().BeTrue();
            VisibilityUtils.SoundBlockFilter(AnyPos, unknown).Should().BeTrue();
        }

        [Fact]
        public void AirBlocksNothing()
        {
            var air = new Block { BlockId = 0 };

            VisibilityUtils.SightBlockFilter(AnyPos, air).Should().BeFalse();
            VisibilityUtils.StrictSightBlockFilter(AnyPos, air).Should().BeFalse();
            VisibilityUtils.SoundBlockFilter(AnyPos, air).Should().BeFalse();
        }
    }
}
