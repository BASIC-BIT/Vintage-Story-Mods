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
    /// A COUNTERWEIGHTED SHAFT COSTS THE NETWORK WHAT A LEVEL ROPEWAY COSTS IT, and the climb term is
    /// CANCELLED rather than discounted. <c>ClimbLoad * climb</c> is the cost of lifting the car's own mass up
    /// the grade; a counterweight is exactly that mass hung on the other strand, so what the drive lifts is the
    /// imbalance - which is <c>cargo</c>, and <c>cargo</c> is 0 until cargo weight lands. So
    /// <c>BEPylonBase.DeclareLoad</c> passes 0 for the climb on a shaft and there is no new constant anywhere.
    /// <para>
    /// STATE THE BALANCE DECISION rather than presenting it as a maintenance economy, because it is not
    /// neutral: the shaft is strictly cheaper on power than a steep ropeway, strictly steeper, and a third of
    /// the placed blocks. What it costs more of is haul rope - <c>ropePerBlock</c> 0.5 against 0.25, in JSON -
    /// and that is the one lever free to move if it turns out to be too strong.
    /// </para>
    /// </summary>
    [Fact]
    public void ACounterweightedShaftCostsTheNetworkWhatALevelLineCosts()
    {
        var level = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);
        var shaft = RopewayPower.Resistance(hauling: true, climb: 0, cargo: 0);
        var bare = RopewayPower.Resistance(hauling: true, climb: 1, cargo: 0);

        Assert.Equal(level, shaft);
        Assert.Equal(RopewayPower.HaulResistance, shaft, 6);
        Assert.True(bare > shaft, "an uncounterweighted vertical line is dearer, or the term never existed");

        // Descending already costs the level figure today, because Resistance clamps a negative climb to zero.
        // So the counterweight makes going UP cost what coming DOWN already costs - symmetric, and no case.
        Assert.Equal(shaft, RopewayPower.Resistance(hauling: true, climb: -1, cargo: 0), 6);

        // WHERE IT LANDS ON THE LADDER, in blocks of RISE per second - on a vertical leg blocks along the line
        // ARE blocks of rise, so there is no sinus anywhere. The point of the counterweight is the BOTTOM rung:
        // a bare vertical line stalls the first mill a player builds and a counterweighted one walks.
        Assert.Equal(0, WindmillSpeed(3, metal: false, (float)bare), 6);
        Assert.InRange(WindmillSpeed(3, metal: false, (float)shaft), 1.0, 1.5);

        // ...and it is a KNIFE EDGE at three sails, which the headline has to say out loud. TargetSpeed is
        // min(0.6, windSpeed), so any wind under 0.4 stalls a 3-sail mill outright, and
        // BEBehaviorWindmillRotor's turbulenceExposed halves the torque factor - which takes a 3-sail mill
        // below zero even counterweighted. The tier a player can rely on is four.
        var turbulent = RopewayPower.CabinSpeed(FullWind - shaft / (3 / 4.0 * 0.5));
        Assert.Equal(0, turbulent, 6);
        Assert.True(WindmillSpeed(4, metal: false, (float)shaft) > 1.5, "a four-sail mill is the reliable tier");

        // And the whole ladder is the LEVEL ladder, unchanged, so RopewayPowerTests keeps one table.
        foreach (var sails in new[] { 2, 3, 4, 5 })
        {
            Assert.Equal(WindmillSpeed(sails, metal: false, (float)level),
                WindmillSpeed(sails, metal: false, (float)shaft), 6);
        }

        // IS THE SHAFT REDUNDANT NEXT TO A 70 DEGREE ROPEWAY SPAN? No, and this is the arithmetic that says
        // so - the surviving objection from ELEVATOR-CHALLENGE, answered in numbers rather than in prose.
        // It is asserted here because the prose got it WRONG: handbook page 53 and QA step 3 claimed a 70
        // degree span climbs at "96% of a shaft's rate" until 2026-08-10, which is POWER-AND-STORAGE's
        // `rise per block travelled` column (sin 70 = 0.94) transposed into a rate it is not. The two
        // effects COMPOUND - the steep span turns 0.94 blocks of travel into a block of rise AND pays 0.441
        // where the shaft pays 0.300 - so 0.94 is a ceiling reached only as torque goes to infinity.
        var steep = RopewayPower.Resistance(hauling: true, climb: Math.Sin(70 * Math.PI / 180), cargo: 0);
        Assert.Equal(0.441, steep, 3);

        double Climb(int sails, bool metal, float load, double rise)
        {
            return WindmillSpeed(sails, metal, load) * rise;
        }

        // Height gained per second, as a fraction of the shaft's, at each rung of the ladder above.
        var rise70 = Math.Sin(70 * Math.PI / 180);
        Assert.Equal(0.50, Climb(4, false, steep, rise70) / Climb(4, false, (float)shaft, 1), 2);
        Assert.Equal(0.86, Climb(10, true, steep, rise70) / Climb(10, true, (float)shaft, 1), 2);

        // THE ROW THAT DECIDES IT: at the tier the docs call the one to rely on, the shaft climbs TWICE as
        // fast, and at three sails the steep ropeway is all but stalled where the shaft still walks.
        Assert.True(Climb(4, false, (float)shaft, 1) > 1.9 * Climb(4, false, steep, rise70));
        Assert.InRange(Climb(3, false, steep, rise70) / Climb(3, false, (float)shaft, 1), 0.0, 0.10);

        // And no mill anywhere reaches the number the prose used to quote.
        Assert.True(Climb(10, true, steep, rise70) / Climb(10, true, (float)shaft, 1) < rise70,
            "a 70 degree span cannot beat sin 70, and it does not reach it either");
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

    // ------------------------------------------------------------------ which tower tensions a line

    /// <summary>
    /// THE structural rule, and the whole of what replaced the tensioner's proximity subsystem. A line has
    /// its tensioner iff one of the towers ON ITS OWN CHAIN is a finished tension station - a walk over a
    /// list the caller already holds, with no radius, no distance, no dimension term and no position table.
    /// <para>
    /// Five tests died here and they are worth naming, because each one existed to hold up a different
    /// corner of the machinery this deletes: a squared distance with a dimension term; height counting
    /// toward a radius; which of two nearby lines a free-standing housing decided to drive; a bare scouting
    /// footing stealing that decision; and two equidistant footings needing a positional tie-break so two
    /// clients could not disagree about the same rope. Membership answers all five by construction. The two
    /// assertions below are the ones that have subjects now.
    /// </para>
    /// </summary>
    [Fact]
    public void ALineIsTensionedByAStationAndNotByProximity()
    {
        var modSystem = new RopewayModSystem();
        var near = new BlockPos(0, 64, 0);
        var far = new BlockPos(0, 64, 40);

        Tower(modSystem, near, far);
        var station = Tower(modSystem, far, near);
        var line = RopewayLine.GetOrBuild(modSystem, near)!;

        // A finished tension station standing ONE BLOCK off the line answers for nothing. Under the old rule
        // it answered for every line with a tower within eight blocks of it, including lines it was never
        // built for; the question is membership of the chain, and this tower is not on it.
        var offLine = Tower(modSystem, new BlockPos(1, 64, 0));
        offLine.IsTensioner = true;
        offLine.StructureComplete = true;
        Assert.False(BEPylonBase.HasTensioner(modSystem, line));

        // A half-built tension station is not a tensioner. Proximity could not express this at all - a
        // weight was either in range or not - and the tower's own overlay says which cells are missing.
        station.IsTensioner = true;
        Assert.False(BEPylonBase.HasTensioner(modSystem, line));

        station.StructureComplete = true;
        Assert.True(BEPylonBase.HasTensioner(modSystem, line));

        // A tower of the line that is finished but is not a tension station is not one either, which is the
        // half that stops "the line is built" from quietly meaning "the line is tensioned".
        modSystem.LoadedTowers.Clear();
        Tower(modSystem, near, far).StructureComplete = true;
        Tower(modSystem, far, near).StructureComplete = true;
        Assert.False(BEPylonBase.HasTensioner(modSystem, RopewayLine.GetOrBuild(modSystem, near)!));
    }

    /// <summary>A loaded footing carrying the spans it is linked by. Enough for GetOrBuild to walk it.</summary>
    private static BEPylonBase Tower(RopewayModSystem modSystem, BlockPos pos, params BlockPos[] spans)
    {
        var tower = new BEPylonBase();
        foreach (var span in spans) tower.Spans.Add(span);
        modSystem.LoadedTowers[pos] = tower;
        return tower;
    }
}
