using System;
using System.Collections.Generic;

namespace Ropeway;

/// <summary>
/// The ropeway as an ordinary mechanical load: what the haul rope costs the network to turn, and how fast
/// the network turning it moves the cabin. Pure, so this is the tested half - the network itself is
/// vanilla's and is not ours to test.
/// <para>
/// There is no store, no charge and no gate. The cabin is driven at the speed the network is running at,
/// exactly like a quern is ground at the speed the network is running at, and a network that stops means a
/// cabin that stops. That is ordinary machine behaviour rather than a failure state because a rider is
/// never trapped: a stalled cabin carries on by itself when the network turns again and stops at a tower,
/// and that is the exit.
/// </para>
/// <para>
/// This used to say "the sneak-hold bail-out gets them out of a stopped cabin anywhere on the line", and
/// that sentence is false at both ends now. <see cref="RopewayCabinSeat.CanUnmount"/> refuses the ordinary
/// step out with nothing under the cabin, and <c>EntityRopewayCabin.BailOut</c> arms the hold off
/// <c>IsMoving</c>, so a stall closes both doors until the line moves. Not trapped, but waiting - which is
/// what the refusal message says in those words. See docs/KNOWN-ISSUES.md.
/// </para>
/// </summary>
public static class RopewayPower
{
    /// <summary>
    /// Blocks of travel per second at network speed 1.0 - the calibration knob, and the only one. Chosen so
    /// the vanilla drives land on a ladder a player can FEEL rather than measure; see
    /// <see cref="HaulResistance"/> for the arithmetic that produces it.
    /// </summary>
    public const double BlocksPerNetworkSpeed = 6.0;

    /// <summary>
    /// What a moving cabin costs the network, level and empty. THE number that makes the drive legible, and
    /// it is large on purpose.
    /// <para>
    /// A rotor settles where its torque meets the load: <c>BEBehaviorMPRotor.GetTorque</c> supplies
    /// <c>(capableSpeed - speed) * TorqueFactor</c> against our resistance, so in good wind
    /// (<c>TargetSpeed = min(0.6, windSpeed)</c>) the network settles at <b>s* = 0.6 - R/T</b> with
    /// <c>T = sails/4 * powerMul</c> (1 wood, 1.25 metal). A SMALL R makes that expression nearly flat -
    /// at the old 0.12 a 3-sail mill and a 10-sail one were 0.44 and 0.56, and no player can feel 22%.
    /// At 0.3 the ladder opens out, in blocks per second at <see cref="BlocksPerNetworkSpeed"/>:
    /// </para>
    /// <list type="bullet">
    /// <item>2-sail wood - stalls. 0.6 x 0.5 &lt; 0.3, the mill cannot shift the cabin at all.</item>
    /// <item>3-sail wood - s* 0.20, <b>1.2 blocks/s</b>, a walk.</item>
    /// <item>5-sail wood (maxed) - s* 0.36, <b>2.2 blocks/s</b>, the old fixed speed.</item>
    /// <item>10-sail metal (maxed) - s* 0.50, <b>3.0 blocks/s</b>, a run.</item>
    /// </list>
    /// <para>
    /// Three times a quern's 0.1 and 40% of a maxed wood mill's 0.75 stall budget: a ropeway is a serious
    /// machine that one good mill drives with room for a quern beside it, and that a small mill does not
    /// drive at all. Vanilla's own numbers throughout - nothing here is invented.
    /// </para>
    /// </summary>
    public const float HaulResistance = 0.3f;

    /// <summary>
    /// Load when there is nothing to haul. The pulverizer's idle/loaded shape (0.005 / 0.085): a parked
    /// ropeway must not permanently tax the mill it shares a network with.
    /// </summary>
    public const float IdleResistance = 0.005f;

    /// <summary>
    /// Extra load per unit of climb, where climb is the vertical component of the unit direction the cabin
    /// is travelling - so 1.0 would be straight up and 0.5 is a 30 degree span. Half, which puts a 30 degree
    /// climb at +25% load (a maxed wood mill: 2.2 -&gt; 1.8 blocks/s) and a 45 degree one at +35% (-&gt; 1.6).
    /// Visible, and never enough on its own to stall a mill that moves the cabin on the level: a cabin that
    /// crawls up the hill is a machine working, a cabin that stops halfway up is a bug report.
    /// </summary>
    public const double ClimbLoad = 0.5;

    /// <summary>
    /// How fast a cabin runs on a network turning at <paramref name="networkSpeed"/>. Zero in, zero out -
    /// the cabin stands still and waits, which is not an error and needs no state of its own.
    /// </summary>
    public static double CabinSpeed(double networkSpeed)
    {
        if (double.IsNaN(networkSpeed) || networkSpeed <= 0) return 0;
        return BlocksPerNetworkSpeed * networkSpeed;
    }

    /// <summary>
    /// The line's drive speed, pooled from its towers' hookups: one contribution per NETWORK, never one per
    /// tower. That distinction is the whole function. Two footings tapped off the same axle run are two
    /// windows onto ONE drive - <c>TrueSpeed</c> is <c>|Network.Speed * GearedRatio|</c>, so they report the
    /// same turning shaft twice - and adding them would mean a player could buy speed with axles: each extra
    /// hookup declares another <see cref="HaulResistance"/> on that network, but the load only SUBTRACTS from
    /// the settling speed while the naive sum MULTIPLIES what gets read, so three stubs off one maxed metal
    /// mill read 5.6 blocks/s against a single hookup's 3.0. Adding load may never make the machine faster.
    /// <para>
    /// Genuinely separate networks - a mill at each end - still add, which is the pooling the design wants.
    /// First hookup seen on a network wins: they are all looking at one rope, so a geared stub does not get
    /// to speak for it, and gearing stays self-correcting because that stub's resistance still lands on the
    /// network (<c>MechanicalNetwork.updateNetwork</c> weights resistance by <c>|gearedRatio|</c>).
    /// </para>
    /// </summary>
    public static double PoolSpeed(IEnumerable<(long NetworkId, double Speed)> drives)
    {
        if (drives == null) return 0;

        var counted = new HashSet<long>();
        var total = 0.0;
        foreach (var (networkId, speed) in drives)
        {
            // A tower with no axle contributes nothing AND must not take a network's slot with its zero.
            if (double.IsNaN(speed) || speed <= 0) continue;
            if (counted.Add(networkId)) total += speed;
        }

        return total;
    }

    /// <summary>
    /// What a tower declares to its network. <paramref name="hauling"/> is the line having a cabin that is
    /// trying to move - NOT one that is actually moving, which would be a feedback loop: the load is what
    /// slows the network, so dropping it the moment the cabin stalls would speed the network up, start the
    /// cabin, and stall it again a tick later.
    /// <para>
    /// <paramref name="climb"/> is the vertical component of the direction of travel, negative downhill and
    /// clamped away - gravity assisting a descent is a real effect but a negative load is not, and a haul
    /// loop carries a cabin each way in any case. <paramref name="cargo"/> is the extra load carried as a
    /// FRACTION of the empty cabin's, and is 0 until cargo weight is designed; it is a parameter rather than
    /// an absent term so that the day it lands, it lands here and nowhere else.
    /// </para>
    /// </summary>
    public static float Resistance(bool hauling, double climb, double cargo)
    {
        if (!hauling) return IdleResistance;

        if (double.IsNaN(climb) || climb < 0) climb = 0;
        if (double.IsNaN(cargo) || cargo < 0) cargo = 0;

        return (float)(HaulResistance * (1 + ClimbLoad * climb + cargo));
    }
}
