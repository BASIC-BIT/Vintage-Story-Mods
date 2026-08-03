using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Ropeway.Tests;

/// <summary>
/// The pure half of mechanical power: what a powered tower banks, what a trip costs, and whether the store
/// can pay for it. The vanilla network itself is not tested here - it is not ours, and a test of it would
/// be a test of the decompiler.
/// <para>
/// Relational rather than absolute wherever the number is balance rather than law, so rebalancing does not
/// break the suite but breaking the ORDERING does. The absolutes that ARE asserted - a level trip costing
/// exactly its length, a refused trip changing nothing - are the invariants the design rests on.
/// </para>
/// </summary>
public class RopewayPowerTests
{
    // ------------------------------------------------------------------ winding

    [Fact]
    public void WindingBanksSpeedTimesRateAndStopsDeadAtCapacity()
    {
        Assert.Equal(3.0, RopewayPower.Wind(0, 400, 1.0, 1.0), 6);
        Assert.Equal(1.5, RopewayPower.Wind(0, 400, 0.5, 1.0), 6);
        Assert.Equal(3.0, RopewayPower.Wind(0, 400, 0.5, 2.0), 6);

        // A full store takes nothing more, which is also what drops the tower back to idle resistance.
        Assert.Equal(400, RopewayPower.Wind(399, 400, 1.0, 10.0), 6);
        Assert.Equal(400, RopewayPower.Wind(400, 400, 1.0, 1.0), 6);
    }

    [Fact]
    public void AnUnpoweredOrStalledTowerBanksNothingAndBreaksNothing()
    {
        Assert.Equal(100, RopewayPower.Wind(100, 400, 0, 1.0), 6);
        Assert.Equal(100, RopewayPower.Wind(100, 400, -1, 1.0), 6);
        Assert.Equal(100, RopewayPower.Wind(100, 400, 1.0, 0), 6);
        Assert.Equal(100, RopewayPower.Wind(100, 400, double.NaN, 1.0), 6);
        Assert.Equal(0, RopewayPower.Wind(100, 0, 1.0, 1.0), 6);
    }

    /// <summary>
    /// POOLING, which is the whole of "any tower can take power and contributions add up": the rate is
    /// linear in speed, so winding twice at 0.3 lands exactly where winding once at 0.6 does. That is why
    /// no tower has to know about any other one, and why a tower whose chunk unloads just stops
    /// contributing instead of desynchronising anything.
    /// </summary>
    [Fact]
    public void ContributionsFromSeveralTowersPoolIntoOneStore()
    {
        var pooled = RopewayPower.Wind(RopewayPower.Wind(0, 400, 0.3, 1.0), 400, 0.3, 1.0);
        var single = RopewayPower.Wind(0, 400, 0.6, 1.0);
        Assert.Equal(single, pooled, 6);

        // Order is irrelevant too - a slow tower first or a fast one first is the same store.
        var slowFirst = RopewayPower.Wind(RopewayPower.Wind(0, 400, 0.2, 1.0), 400, 0.9, 1.0);
        var fastFirst = RopewayPower.Wind(RopewayPower.Wind(0, 400, 0.9, 1.0), 400, 0.2, 1.0);
        Assert.Equal(slowFirst, fastFirst, 6);
    }

    /// <summary>
    /// The one number worth anchoring absolutely, and it is anchored on vanilla rather than invented: a
    /// maxed five-sail wood windmill winding against WindingResistance settles near Speed 0.5
    /// (s* = 0.6 - 0.12/1.25), which has to fund a level 100-block trip in roughly the time the trip itself
    /// takes. Miss this by an order of magnitude and the mod is either a wait or a formality.
    /// </summary>
    [Fact]
    public void AMaxedWoodWindmillFundsALevelHundredBlockTripInAboutAMinute()
    {
        const double equilibriumSpeed = 0.6 - RopewayPower.WindingResistance / 1.25;

        var stored = 0.0;
        var seconds = 0;
        var cost = RopewayPower.Quote(100, 0);

        while (!RopewayPower.CanAfford(stored, cost) && seconds < 600)
        {
            stored = RopewayPower.Wind(stored, RopewayPower.DefaultCapacity, equilibriumSpeed, 1.0);
            seconds++;
        }

        Assert.InRange(seconds, 40, 120);
    }

    [Fact]
    public void WindingLoadsTheNetworkAndIdlingBarelyDoes()
    {
        // Above the quern's 0.1 so a ropeway reads as a serious machine, under a maxed wood windmill's
        // 0.75 stall budget so one mill still drives several towers.
        Assert.InRange(RopewayPower.WindingResistance, 0.1f, 0.2f);

        // And an order of magnitude below it when there is nothing to wind - a finished ropeway must not
        // permanently tax the mill it shares a network with.
        Assert.True(RopewayPower.IdleResistance < RopewayPower.WindingResistance / 10);
    }

    // ------------------------------------------------------------------ the quote

    [Theory]
    [InlineData(100, 0, 100)]      // level: the trip costs its own length
    [InlineData(100, 40, 180)]     // climbing 40: length + 2 per block of rise
    [InlineData(100, -40, 60)]     // descending 40: length - 1 per block of drop
    [InlineData(100, -90, 25)]     // steep descent, floored at a quarter of the length
    [InlineData(0, 50, 0)]         // no distance, no charge, whatever the geometry claims
    public void TheQuoteChargesDistanceDearerUphillAndCheaperDown(double length, double climb, double expected)
    {
        Assert.Equal(expected, RopewayPower.Quote(length, climb), 6);
    }

    [Fact]
    public void NoTripIsEverFreeNoMatterHowSteeplyItFalls()
    {
        for (var drop = 0.0; drop < 400; drop += 7)
        {
            var cost = RopewayPower.Quote(100, -drop);
            Assert.True(cost >= RopewayPower.MinCostFraction * 100, $"drop {drop} priced at {cost}");
        }
    }

    [Fact]
    public void ClimbingIsAlwaysDearerThanLevelAndLevelDearerThanDescending()
    {
        var climb = RopewayPower.Quote(120, 30);
        var level = RopewayPower.Quote(120, 0);
        var descend = RopewayPower.Quote(120, -30);

        Assert.True(climb > level, $"{climb} !> {level}");
        Assert.True(level > descend, $"{level} !> {descend}");
    }

    // ------------------------------------------------------------------ paying

    [Fact]
    public void AStoreEitherPaysTheWholeQuoteOrRefusesAndChangesNothing()
    {
        var store = new BETensionWeight { Charge = 180 };

        Assert.True(store.TrySpend(180));
        Assert.Equal(0, store.Charge, 6);

        store.Charge = 179.9;
        Assert.False(store.TrySpend(180));

        // THE invariant: a refused trip leaves the store exactly as it was. A partial payment would be a
        // cabin leaving with less energy than its journey needs, which is the stranding case the whole
        // store exists to make impossible.
        Assert.Equal(179.9, store.Charge, 6);
    }

    [Fact]
    public void APricelessTripIsRefusedRatherThanGivenAway()
    {
        Assert.False(RopewayPower.CanAfford(400, double.NaN));
        Assert.False(RopewayPower.CanAfford(double.NaN, 10));
        Assert.True(RopewayPower.CanAfford(0, 0));
    }

    [Fact]
    public void TheStoreClampsToItsCapacityInBothDirections()
    {
        var store = new BETensionWeight();

        store.Wind(10, 100);
        Assert.Equal(RopewayPower.DefaultCapacity, store.Charge, 6);
        Assert.True(store.Full);
        Assert.Equal(1, store.Fraction, 6);

        Assert.True(store.TrySpend(RopewayPower.DefaultCapacity));
        Assert.Equal(0, store.Charge, 6);
        Assert.False(store.Full);
    }

    // ------------------------------------------------------------------ quoting a real line

    /// <summary>
    /// The quote read off actual line geometry, which is where a sign error would land: a downhill ore line
    /// that charges double and an uphill one that runs free is the same bug twice.
    /// </summary>
    [Fact]
    public void TripCostTakesTheClimbFromTheLineItself()
    {
        var low = new BlockPos(0, 64, 0);
        var high = new BlockPos(0, 104, 0);
        var line = RopewayLine.FromTowers(new List<BlockPos> { low, high });

        var length = line.TotalLength;
        var up = EntityRopewayCabin.TripCost(line, 0, length);
        var down = EntityRopewayCabin.TripCost(line, length, 0);

        Assert.Equal(RopewayPower.Quote(length, 40), up, 6);
        Assert.Equal(RopewayPower.Quote(length, -40), down, 6);
        Assert.True(up > down);
    }

    [Fact]
    public void ALevelLineCostsTheSameEitherWayAndHalfTripsCostHalf()
    {
        var line = RopewayLine.FromTowers(new List<BlockPos>
        {
            new BlockPos(0, 64, 0),
            new BlockPos(100, 64, 0)
        });

        var whole = EntityRopewayCabin.TripCost(line, 0, line.TotalLength);
        Assert.Equal(whole, EntityRopewayCabin.TripCost(line, line.TotalLength, 0), 6);
        Assert.Equal(whole / 2, EntityRopewayCabin.TripCost(line, 0, line.TotalLength / 2), 6);
        Assert.Equal(0, EntityRopewayCabin.TripCost(null, 0, 10), 6);
    }

    // ------------------------------------------------------------------ the trip credit

    private static RopewayLine LevelLine()
    {
        return RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, 0), new(300, 64, 0) });
    }

    /// <summary>
    /// THE INVARIANT the whole store design exists for: a trip is paid for ONCE. A cabin stopped short of
    /// the reach it bought - a blocked span, a chunk that has not landed, a save - carries on for nothing,
    /// and it does so out of an EMPTY store, because the energy left the store before it ever moved.
    /// Charging afresh here is the stranding case reached through the interruption path instead of the
    /// power path: a rider held mid-span with an empty store could only dismount into open air.
    /// </summary>
    [Fact]
    public void AnInterruptedTripFinishesOnWhatItAlreadyPaidEvenWithAnEmptyStore()
    {
        var line = LevelLine();

        var (fare, paidTo) = EntityRopewayCabin.Fare(line, 0, EntityRopewayCabin.NoDestination, line.TotalLength);
        Assert.Equal(line.TotalLength, fare, 6);
        Assert.Equal(line.TotalLength, paidTo, 6);

        // Held halfway, then aimed at the same end again.
        var (resume, stillPaidTo) = EntityRopewayCabin.Fare(line, 137, paidTo, line.TotalLength);
        Assert.Equal(0, resume, 6);
        Assert.Equal(paidTo, stillPaidTo, 6);
        Assert.True(RopewayPower.CanAfford(0, resume), "a paid trip must finish out of an empty store");
    }

    /// <summary>
    /// The other half of paying once: pressing the stop key after departure is a rider changing their mind
    /// inside a journey the store has already funded, and it must not be a second full quote. Only the part
    /// that reaches PAST what was bought costs anything, and it costs exactly the difference.
    /// </summary>
    [Fact]
    public void ReAimingInsideAPaidTripIsFreeAndExtendingItPaysOnlyTheDifference()
    {
        var line = LevelLine();

        // Paid from the start to the 200 mark; the cabin is at 50.
        Assert.Equal(0, EntityRopewayCabin.Fare(line, 50, 200, 120).Cost, 6);
        Assert.Equal(0, EntityRopewayCabin.Fare(line, 50, 200, 200).Cost, 6);

        // The reach does not shrink to meet a nearer stop, so changing back is free too.
        Assert.Equal(200, EntityRopewayCabin.Fare(line, 50, 200, 120).PaidTo, 6);

        var extension = EntityRopewayCabin.Fare(line, 50, 200, 300);
        Assert.Equal(EntityRopewayCabin.TripCost(line, 200, 300), extension.Cost, 6);
        Assert.Equal(300, extension.PaidTo, 6);
    }

    /// <summary>
    /// Turning round is NOT inside the paid trip - it is a new one, from where the cabin stands - and this
    /// is the clause that keeps the credit from funding unlimited travel. The covered stretch only ever
    /// shrinks as the cabin runs into it, so no amount of re-aiming buys more than was paid for.
    /// </summary>
    [Fact]
    public void TurningRoundIsANewTripAndIsQuotedAfresh()
    {
        var line = LevelLine();

        var back = EntityRopewayCabin.Fare(line, 100, 300, 0);
        Assert.Equal(EntityRopewayCabin.TripCost(line, 100, 0), back.Cost, 6);
        Assert.Equal(0, back.PaidTo, 6);

        // And with nothing paid at all, every aim is the full quote.
        Assert.Equal(
            EntityRopewayCabin.TripCost(line, 0, 300),
            EntityRopewayCabin.Fare(line, 0, EntityRopewayCabin.NoDestination, 300).Cost,
            6);
    }

    // ------------------------------------------------------------------ what a line may cost at all

    /// <summary>
    /// The number the link gate weighs against the store's capacity has to be the TRUE worst case, or it
    /// lets an unrunnable leg through: a line that climbs and then falls quotes its net climb end to end
    /// while the uphill half on its own is far dearer.
    /// </summary>
    [Fact]
    public void TheWorstTripOnALineIsNotAlwaysTheEndToEndOne()
    {
        var line = RopewayLine.FromTowers(new List<BlockPos>
        {
            new(0, 64, 0),
            new(20, 164, 0),
            new(40, 64, 0)
        });

        var endToEnd = EntityRopewayCabin.TripCost(line, 0, line.TotalLength);
        var uphillHalf = EntityRopewayCabin.TripCost(line, 0, line.Cumulative[1]);
        var worst = EntityRopewayCabin.WorstTripCost(line);

        Assert.True(uphillHalf > endToEnd, $"{uphillHalf} !> {endToEnd}");
        Assert.Equal(uphillHalf, worst, 6);
        Assert.Equal(0, EntityRopewayCabin.WorstTripCost(null), 6);
    }

    /// <summary>
    /// The link gate needs the merged geometry BEFORE the span exists, so the preview has to join the two
    /// chains at the towers being linked whichever end of its own line each of them happens to sit on.
    /// </summary>
    [Fact]
    public void PreviewJoinsTwoChainsAtTheTowersBeingLinkedFromEitherEnd()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(50, 64, 0);
        var c = new BlockPos(90, 64, 0);
        var d = new BlockPos(140, 64, 0);

        var left = RopewayLine.FromTowers(new List<BlockPos> { a, b });
        var right = RopewayLine.FromTowers(new List<BlockPos> { c, d });

        var merged = RopewayLine.Preview(left, b, right, c);
        Assert.Equal(new[] { a, b, c, d }, merged.Towers);
        Assert.Equal(left.TotalLength + 40 + right.TotalLength, merged.TotalLength, 6);

        // Linking the far ends instead reverses both chains rather than building a chain that doubles back.
        var flipped = RopewayLine.Preview(left, a, right, d);
        Assert.Equal(new[] { b, a, d, c }, flipped.Towers);

        // A tower with no line of its own is a chain of one.
        Assert.Equal(new[] { a, c, d }, RopewayLine.Preview(null, a, right, c).Towers);
    }

    // ------------------------------------------------------------------ where the weight binds

    /// <summary>
    /// Binding picks the NEAREST tower, and the dimension term is the one with no visible symptom until
    /// somebody builds a ropeway in a pocket dimension - at which point a weight binds through the floor of
    /// the world to a line it has nothing to do with.
    /// </summary>
    [Fact]
    public void AWeightBindsOnlyToTowersInRangeAndInItsOwnDimension()
    {
        var weight = new BlockPos(0, 64, 0);

        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(weight, new BlockPos(9, 64, 0), 8));
        Assert.Equal(64, BlockTensionWeight.Nearest(weight, new BlockPos(8, 64, 0), 8), 6);
        Assert.Equal(0, BlockTensionWeight.Nearest(weight, new BlockPos(0, 64, 0), 8), 6);

        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(weight, new BlockPos(1, 64, 0, 2), 8));
        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(weight, null, 8));
        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(null, weight, 8));
    }

    [Fact]
    public void HeightCountsTowardTheBindingRadius()
    {
        // Or a weight at the bottom of a shaft binds to a tower on the cliff directly above it.
        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(new BlockPos(0, 40, 0), new BlockPos(0, 64, 0), 8));
    }

    // ------------------------------------------------------------------ the gauge

    /// <summary>
    /// The drawn mass IS the gauge, so its failure mode is a block that renders nothing at all, silently -
    /// the same pair of CubeMeshUtil traps the cable documents. Face count and colour maps are what the
    /// chunk tesselator indexes; without them the mesh is either invisible or an IndexOutOfRangeException.
    /// </summary>
    [Fact]
    public void TheDrawnMassCarriesTheFacesAndColourMapsTheTesselatorIndexes()
    {
        var mesh = BETensionWeight.BuildMass(1.5f, new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 });

        Assert.NotNull(mesh);
        Assert.Equal(6, mesh.XyzFacesCount);
        Assert.True(mesh.SeasonColorMapIds.Length >= 6);
        Assert.True(mesh.ClimateColorMapIds.Length >= 6);
    }

    [Fact]
    public void TheDrawnMassSitsWhereItsChargePutsIt()
    {
        var low = Centre(BETensionWeight.BuildMass(0.5f, Atlas()));
        var high = Centre(BETensionWeight.BuildMass(2.5f, Atlas()));

        Assert.Equal(0.5, low, 3);
        Assert.Equal(2.5, high, 3);
    }

    private static TextureAtlasPosition Atlas() => new() { x1 = 0, y1 = 0, x2 = 1, y2 = 1 };

    private static double Centre(Vintagestory.API.Client.MeshData mesh)
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var i = 0; i < mesh.VerticesCount; i++)
        {
            min = System.Math.Min(min, mesh.xyz[3 * i + 1]);
            max = System.Math.Max(max, mesh.xyz[3 * i + 1]);
        }

        return (min + max) / 2;
    }
}
