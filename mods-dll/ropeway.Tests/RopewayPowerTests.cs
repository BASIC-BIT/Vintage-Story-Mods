using Vintagestory.API.MathTools;

namespace Ropeway.Tests;

/// <summary>
/// The pure half of mechanical power: what the haul rope costs a network to turn, and how fast a network
/// turning it moves the cabin. The vanilla network itself is not tested here - it is not ours, and a test of
/// it would be a test of the decompiler.
/// <para>
/// The equilibrium the ladder tests use is vanilla's, not ours: <c>BEBehaviorMPRotor.GetTorque</c> supplies
/// <c>(capableSpeed - speed) * TorqueFactor</c> against the network's resistance, so a rotor with torque
/// factor T settles at <c>s* = TargetSpeed - R/T</c>. For a windmill <c>TargetSpeed = min(0.6, windSpeed)</c>
/// and <c>T = sails/4 * powerMul</c> (powerMul 1 wood, 1.25 metal). Reproduced here rather than imported,
/// because the point of these tests is that the ladder a player feels comes out of vanilla's arithmetic.
/// </para>
/// </summary>
public class RopewayPowerTests
{
    private const double FullWind = 0.6;

    /// <summary>Blocks per second a windmill of this many sails drives a cabin at, against a given load.</summary>
    private static double WindmillSpeed(int sails, bool metal, float resistance)
    {
        var torque = sails / 4.0 * (metal ? 1.25 : 1.0);
        return RopewayPower.CabinSpeed(FullWind - resistance / torque);
    }

    // ------------------------------------------------------------------ the ladder

    /// <summary>
    /// THE reason <see cref="RopewayPower.HaulResistance"/> is large. The old design's 0.12 put three sails
    /// and ten within 22% of each other, which no player can feel; the whole point of the load model is that
    /// the size of your mill is visible in the speed of your cabin.
    /// </summary>
    [Fact]
    public void ABiggerDriveIsAVisiblyFasterCabin()
    {
        var load = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);

        var three = WindmillSpeed(3, metal: false, load);
        var five = WindmillSpeed(5, metal: false, load);
        var ten = WindmillSpeed(10, metal: true, load);

        // Not merely ordered - separated. Half again from three sails to five, and again to a metal rotor.
        Assert.True(five > three * 1.5, $"5 sails {five} is not visibly faster than 3 sails {three}");
        Assert.True(ten > five * 1.3, $"a maxed metal rotor {ten} is not visibly faster than 5 wood sails {five}");

        // And the absolute rungs, because "legible" means these numbers and not just their order: a walk,
        // the speed the cabin used to be nailed to, and a run.
        Assert.InRange(three, 1.0, 1.5);
        Assert.InRange(five, 2.0, 2.5);
        Assert.InRange(ten, 2.8, 3.4);
    }

    /// <summary>
    /// The bottom of the ladder is a stall, and that is the design: a two-sail mill cannot haul a cabin, so
    /// the answer to a cabin that will not move is a bigger mill rather than a longer wait.
    /// </summary>
    [Fact]
    public void AMillTooSmallToHaulTheCabinSimplyDoesNotMoveIt()
    {
        var load = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);

        Assert.Equal(0, WindmillSpeed(2, metal: false, load), 6);
        Assert.Equal(0, RopewayPower.CabinSpeed(0), 6);
        Assert.Equal(0, RopewayPower.CabinSpeed(-1), 6);
        Assert.Equal(0, RopewayPower.CabinSpeed(double.NaN), 6);
    }

    /// <summary>
    /// Pooling, which is the whole of "power may be supplied at any tower and contributions add up". It is
    /// addition, so it needs no coordination and cannot desync when one tower's chunk unloads.
    /// </summary>
    [Fact]
    public void TwoDrivesOnALinePoolIntoOneSpeed()
    {
        Assert.Equal(RopewayPower.CabinSpeed(0.6), RopewayPower.CabinSpeed(0.2 + 0.4), 6);
        Assert.True(RopewayPower.CabinSpeed(0.5) > RopewayPower.CabinSpeed(0.3));

        // Two mills, two networks, one line: that is the pooling.
        Assert.Equal(0.6, RopewayPower.PoolSpeed(new[] { (1L, 0.2), (2L, 0.4) }), 6);
    }

    /// <summary>
    /// The one thing a load model may never do is get FASTER when you add load. Every tower on a line
    /// declares the haul resistance, so tapping three footings off one axle run puts 3x the load on that
    /// network - and if each of them also reported its network's speed, the reported line speed would go UP,
    /// because the load only subtracts from the settling speed while the sum multiplies what is read. Three
    /// stubs off one maxed metal mill bought +86% cabin speed for the price of some axles.
    /// </summary>
    [Fact]
    public void HookupsOnOneNetworkAreOneDriveNoMatterHowManyOfThemThereAre()
    {
        // A maxed metal rotor, three footings tapped off it: one drive, and the extra load it now carries
        // makes the cabin slower rather than faster.
        var settled = FullWind - Load(3) / 3.125;
        var one = RopewayPower.PoolSpeed(new[] { (7L, FullWind - Load(1) / 3.125) });
        var three = RopewayPower.PoolSpeed(new[] { (7L, settled), (7L, settled), (7L, settled) });

        Assert.Equal(0.504, one, 3);
        Assert.True(three < one, $"three hookups on one network read {three}, faster than one hookup's {one}");

        // ...while genuinely separate networks still add, which is the behaviour the design wants.
        Assert.Equal(one * 2, RopewayPower.PoolSpeed(new[] { (7L, one), (8L, one) }), 6);

        // A tower with an axle that is not turning contributes nothing and cannot speak for its network.
        Assert.Equal(0.5, RopewayPower.PoolSpeed(new[] { (7L, 0.0), (7L, 0.5), (8L, double.NaN) }), 6);
        Assert.Equal(0, RopewayPower.PoolSpeed(null), 6);
    }

    /// <summary>Load on a network with n towers of the line hooked to it, at ratio 1.</summary>
    private static float Load(int towers) => towers * RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);

    // ------------------------------------------------------------------ the load

    [Fact]
    public void AParkedCabinBarelyTaxesTheNetworkAndAMovingOneReallyDoes()
    {
        var idle = RopewayPower.Resistance(hauling: false, climb: 0, cargo: 0);
        var hauling = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);

        Assert.Equal(RopewayPower.IdleResistance, idle);

        // A finished ropeway must not permanently tax the mill it shares a network with...
        Assert.True(idle < hauling / 10);

        // ...and a working one must read as a serious machine: above the quern's 0.1, and inside a maxed
        // wood windmill's 0.75 stall budget with room left for something else on the same network.
        Assert.InRange(hauling, 0.1f, 0.5f);

        // A parked cabin idles whatever the geometry claims - it is not on a slope, it is not moving.
        Assert.Equal(idle, RopewayPower.Resistance(hauling: false, climb: 0.9, cargo: 3));
    }

    /// <summary>
    /// Climb is load, which is what makes a mountain line a different machine from a valley one. Bounded on
    /// purpose: a mill that moves the cabin on the level must still move it up a steep span, or the model
    /// hands the player a cabin stuck halfway up a hill with no way to read why.
    /// </summary>
    [Fact]
    public void ClimbingCostsMoreAndDescendingIsNeverACredit()
    {
        var level = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);
        var gentle = RopewayPower.Resistance(hauling: true, climb: 0.5, cargo: 0);
        var steep = RopewayPower.Resistance(hauling: true, climb: 0.707, cargo: 0);

        Assert.True(gentle > level);
        Assert.True(steep > gentle);

        // Downhill costs the same as level. A negative load would be a network the ropeway DRIVES.
        Assert.Equal(level, RopewayPower.Resistance(hauling: true, climb: -0.7, cargo: 0));

        // The three-sail mill that walks the cabin along the flat still climbs, slowly, rather than stalling.
        Assert.True(WindmillSpeed(3, metal: false, steep) > 0.2, "a 45 degree span stalls the smallest mill that can haul at all");
        Assert.True(WindmillSpeed(5, metal: false, steep) < WindmillSpeed(5, metal: false, level));
    }

    /// <summary>
    /// Cargo has no rule yet and deliberately no effect, but it has a HOME: the day weight lands it lands in
    /// this one function, which is what stops it being invented separately in the cabin and the tower.
    /// </summary>
    [Fact]
    public void CargoIsATermInTheLoadRatherThanASecondRuleWaitingToBeInvented()
    {
        var empty = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);

        Assert.True(RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0.5) > empty);
        Assert.Equal(empty, RopewayPower.Resistance(hauling: true, climb: 0, cargo: -1));
        Assert.Equal(empty, RopewayPower.Resistance(hauling: true, climb: double.NaN, cargo: double.NaN));
    }

    // ------------------------------------------------------------------ where the tensioner may stand

    /// <summary>
    /// The tensioner is a build requirement and the only geometry it has is "within reach of a tower". The
    /// dimension term is the one with no visible symptom until somebody builds a ropeway in a pocket
    /// dimension, at which point a weight answers for a line through the floor of the world.
    /// </summary>
    [Fact]
    public void ATensionerCountsOnlyForTowersInRangeAndInItsOwnDimension()
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
    public void HeightCountsTowardTheTensionerRadius()
    {
        // Or a weight at the bottom of a shaft serves a tower on the cliff directly above it.
        Assert.Equal(double.MaxValue, BlockTensionWeight.Nearest(new BlockPos(0, 40, 0), new BlockPos(0, 64, 0), 8));
    }
}
