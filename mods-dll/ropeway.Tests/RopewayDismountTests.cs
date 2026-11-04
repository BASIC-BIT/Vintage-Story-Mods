namespace Ropeway.Tests;

/// <summary>
/// The permission to step out of a cabin. It used to be "is the cabin moving", and that is the defect these
/// tests exist for: <c>IsMoving</c> goes false the moment the mechanical network stalls, so a rider hanging
/// over a valley in a becalmed cabin got the ORDINARY dismount on one tap of sneak - vanilla's
/// <c>tryTeleportToFreeLocation</c> finds no solid block either side of a mid-span cabin, skips the teleport,
/// and <c>RopewayCabinSeat.DidUnmount</c> re-datums the fall to the seat they were sitting in.
/// <para>
/// The question the seat asks now is whether there is ground under the cabin, and these run it against the
/// SHIPPED tower frame rather than against a made-up height: every Y here is derived from
/// <c>SpanMath.SheaveHeight</c> and <c>EntityRopewayCabin.DefaultHangDrop</c>, so moving either one moves
/// the test with it.
/// </para>
/// </summary>
public class RopewayDismountTests
{
    /// <summary>The footing block of a tower, in the frame the rest of these numbers are measured in.</summary>
    private const int FootingY = 100;

    /// <summary>
    /// Where a cabin's origin hangs when it is standing at that tower: the sheave centre, less the drop.
    /// <c>RopewayAssetContractTests.TheCabinFitsThroughTheTower</c> owns the same arithmetic for clearance.
    /// </summary>
    private static double CabinAtTower => FootingY + SpanMath.SheaveHeight + 0.5 - EntityRopewayCabin.DefaultHangDrop;

    /// <summary>A column with exactly one solid block in it.</summary>
    private static Func<int, bool> GroundAt(int y) => probe => probe == y;

    /// <summary>
    /// THE DEFECT, both halves in one test, because it is the PAIR that is the rule: the same stopped cabin
    /// answers differently depending on what is under it, and nothing here asks whether it is moving.
    /// <para>
    /// On the shipped code both cases came back "step out" - <c>CanUnmount</c> returned <c>!Moving</c>, and a
    /// stalled cabin is not moving wherever it is standing.
    /// </para>
    /// </summary>
    [Fact]
    public void AStalledCabinRefusesTheStepOutMidSpanAndAllowsItAtATower()
    {
        // At a tower: the footing is 2.25 blocks under the cabin's own origin and it is what the rider
        // steps onto. If this ever goes false, a cabin parked at a station lets nobody out.
        Assert.True(RopewayCabinSeat.GroundUnder(CabinAtTower, GroundAt(FootingY)));

        // The same cabin, same stopped state, out over a valley floor forty blocks down.
        Assert.False(RopewayCabinSeat.GroundUnder(CabinAtTower, GroundAt(FootingY - 40)));
    }

    /// <summary>
    /// The threshold is vanilla's own free-fall allowance and not a judgement call - a drop under
    /// <c>3.5 * fallDamageThreshold</c> costs nothing at all (<c>EntityBehaviorHealth.OnFallToGround</c>), so
    /// ground that near is ground worth stepping onto and a refusal there would be a lie about the danger.
    /// A hillside a couple of blocks under a span is the case this protects: it must NOT be refused.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(20, false)]
    public void GroundIsWorthSteppingOntoForAsFarAsVanillaGivesAwayTheFall(int blocksBelow, bool expected)
    {
        // Whole blocks below the cabin's own origin, which is the datum the seat probes from. The last one
        // that counts is 4 down: its top face is 3.25 under the cabin, and the rider's feet are 1.25 lower
        // again, so every answer here is conservative by that much against vanilla's 3.5.
        Assert.Equal(expected, RopewayCabinSeat.GroundUnder(CabinAtTower, GroundAt((int)Math.Floor(CabinAtTower) - blocksBelow)));
    }

    /// <summary>
    /// A column with nothing in it at all is not a maybe. The probe is what the world hands back for an
    /// unloaded chunk as well as for air, and both mean "do not step out here".
    /// </summary>
    [Fact]
    public void AnEmptyColumnIsNeverGround()
    {
        Assert.False(RopewayCabinSeat.GroundUnder(CabinAtTower, _ => false));
        Assert.False(RopewayCabinSeat.GroundUnder(CabinAtTower, null));
    }

    /// <summary>
    /// And the free fall is vanilla's number, stated once here so a silent edit of the constant fails
    /// something rather than quietly widening what the mod calls a survivable step.
    /// </summary>
    [Fact]
    public void TheFreeFallIsVanillasOwnFallDamageThreshold()
    {
        Assert.Equal(3.5, RopewayCabinSeat.FreeFall);
    }
}
