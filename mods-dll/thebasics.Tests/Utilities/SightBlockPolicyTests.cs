using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.Utilities;

public class SightBlockPolicyTests
{
    [Theory]
    [InlineData("thebasics:scene-marker-ground-north")]
    [InlineData("thebasics:scene-marker-wall-east")]
    [InlineData("thebasics:scene-marker-ceiling-south")]
    public void OpaqueSceneMarkerPassesThroughBothSightFiltersByDefault(string code)
    {
        var marker = Block(50, code, EnumChunkRenderPass.OpaqueNoCull);
        var policy = SightBlockPolicy.Resolve([marker], [], []);

        policy.GeneralFilter(new BlockPos(0, 0, 0, 0), marker).Should().BeFalse();
        policy.StrictFilter(new BlockPos(0, 0, 0, 0), marker).Should().BeFalse();
    }

    [Fact]
    public void OrdinaryOpaqueBlockStillStopsBothSightFilters()
    {
        var stone = Block(51, "game:rock-granite", EnumChunkRenderPass.OpaqueNoCull);
        var policy = SightBlockPolicy.Resolve([stone], [], []);

        policy.GeneralFilter(new BlockPos(0, 0, 0, 0), stone).Should().BeTrue();
        policy.StrictFilter(new BlockPos(0, 0, 0, 0), stone).Should().BeTrue();
    }

    [Fact]
    public void ExplicitBlockingPatternStopsOpaqueSceneMarkerInBothSightFilters()
    {
        var marker = Block(52, "thebasics:scene-marker-wall-east", EnumChunkRenderPass.OpaqueNoCull);
        var policy = SightBlockPolicy.Resolve([marker], ["thebasics:scene-marker-*"], ["thebasics:scene-marker-wall-*"]);

        policy.GeneralFilter(new BlockPos(0, 0, 0, 0), marker).Should().BeTrue();
        policy.StrictFilter(new BlockPos(0, 0, 0, 0), marker).Should().BeTrue();
    }

    [Fact]
    public void PassThroughPatternOverridesAnOpaqueModBlock()
    {
        var lattice = Block(41, "decorplus:brass-lattice-east", EnumChunkRenderPass.OpaqueNoCull);
        var policy = SightBlockPolicy.Resolve(
            [lattice],
            passThroughPatterns: ["decorplus:brass-lattice-*"],
            blockingPatterns: []);

        policy.ShouldStop(lattice, foliagePasses: false).Should().BeFalse();
    }

    [Fact]
    public void BlockingPatternWinsOverPassThroughAndTransparentRendering()
    {
        var curtain = Block(42, "decorplus:privacy-curtain-red", EnumChunkRenderPass.Transparent);
        var policy = SightBlockPolicy.Resolve(
            [curtain],
            passThroughPatterns: ["decorplus:privacy-curtain-*"],
            blockingPatterns: ["decorplus:privacy-curtain-red"]);

        policy.ShouldStop(curtain, foliagePasses: true).Should().BeTrue();
    }

    [Fact]
    public void PatternDoesNotMatchTheSamePathInAnotherDomain()
    {
        var intended = Block(43, "decorplus:brass-lattice-east", EnumChunkRenderPass.OpaqueNoCull);
        var unrelated = Block(44, "othermod:brass-lattice-east", EnumChunkRenderPass.OpaqueNoCull);
        var policy = SightBlockPolicy.Resolve(
            [intended, unrelated],
            passThroughPatterns: ["decorplus:brass-lattice-*"],
            blockingPatterns: []);

        policy.ShouldStop(intended, foliagePasses: false).Should().BeFalse();
        policy.ShouldStop(unrelated, foliagePasses: false).Should().BeTrue();
    }

    [Fact]
    public void ResolutionReportsUnmatchedPatternsAndWildcardConflicts()
    {
        var curtain = Block(45, "decorplus:privacy-curtain-red", EnumChunkRenderPass.Transparent);

        var policy = SightBlockPolicy.Resolve(
            [curtain],
            passThroughPatterns: ["decorplus:privacy-curtain-*", "missingmod:ghost-*"],
            blockingPatterns: ["decorplus:*-red"]);

        policy.UnmatchedPatterns.Should().Equal("missingmod:ghost-*");
        policy.ConflictingBlockCodes.Should().Equal("decorplus:privacy-curtain-red");
    }

    [Fact]
    public void ResolutionTreatsMalformedHandEditedPatternsAsUnmatched()
    {
        var block = Block(46, "decorplus:lattice", EnumChunkRenderPass.OpaqueNoCull);

        SightBlockPolicy policy = null!;
        Action action = () => policy = SightBlockPolicy.Resolve(
            [block],
            passThroughPatterns: [":", "unqualified"],
            blockingPatterns: []);

        action.Should().NotThrow();
        policy.UnmatchedPatterns.Should().Equal(":", "unqualified");
    }

    [Fact]
    public void ConfigureSightBlockOverrides_BoundsAndCountsStartupConflictWarnings()
    {
        var blocks = Enumerable.Range(0, 12)
            .Select(index => Block(index + 1, $"game:block-{index:D2}-granite", EnumChunkRenderPass.Transparent))
            .ToArray();
        var config = new ModConfig
        {
            SightPassThroughBlockCodePatterns = ["game:block-*"],
            SightBlockingBlockCodePatterns = ["game:*-granite"]
        };
        var logger = Substitute.For<ILogger>();
        var world = Substitute.For<IWorldAccessor>();
        world.Blocks.Returns(blocks);
        world.Side.Returns(EnumAppSide.Server);
        world.Logger.Returns(logger);

        VisibilityUtils.ConfigureSightBlockOverrides(world, config);

        var warnings = logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Warning))
            .Select(call =>
            {
                var arguments = call.GetArguments();
                var format = arguments[0] as string ?? string.Empty;
                return arguments.Length == 2 && arguments[1] is object[] formatArguments
                    ? string.Format(format, formatArguments)
                    : format;
            })
            .ToArray();
        warnings.Should().ContainSingle();
        warnings[0].Should().Contain("12 blocks");
        warnings[0].Should().Contain("game:block-09-granite");
        warnings[0].Should().NotContain("game:block-10-granite");
    }

    private static Block Block(int id, string code, EnumChunkRenderPass renderPass) => new()
    {
        BlockId = id,
        Code = new AssetLocation(code),
        BlockMaterial = EnumBlockMaterial.Metal,
        RenderPass = renderPass,
        SelectionBoxes = [new Cuboidf(0, 0, 0, 1, 1, 1)]
    };
}
