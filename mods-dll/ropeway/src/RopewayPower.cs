using System;

namespace Ropeway;

/// <summary>
/// The wound-store arithmetic: what a powered tower banks, what a trip costs, and whether the store can
/// pay for it. No engine state, so this is the tested half of mechanical power - the network itself is
/// vanilla's and is not ours to test.
/// <para>
/// The unit throughout is <em>blocks of travel</em>. That is deliberate: it makes the quote, the store's
/// capacity and the line's own length the same number, so "the weight holds 118 and this trip needs 180"
/// is a sentence a player can act on without learning a second currency.
/// </para>
/// </summary>
public static class RopewayPower
{
    /// <summary>
    /// Blocks of travel banked per second at <c>TrueSpeed</c> 1.0. Anchored on vanilla, not invented: a
    /// maxed 5-sail wood windmill winding against <see cref="WindingResistance"/> settles near Speed 0.5
    /// (s* = 0.6 - 0.12/1.25), so it banks ~1.5 blocks/s and funds a level 100-block trip in about 66
    /// seconds against the 45 seconds the trip itself takes. That ~60% duty cycle is the intended rhythm.
    /// </summary>
    public const double ChargePerSpeedSecond = 3.0;

    /// <summary>
    /// Default store size, in blocks of travel. Deliberately a constant and NOT a function of the line's
    /// length: <see cref="RopewayLine.TotalLength"/> is derived from which chunks are loaded, and a battery
    /// that changed size with the chunk view would walk the truncated-line failure class straight into the
    /// power system. Overridable per blocktype via <c>attributes.capacity</c>.
    /// </summary>
    public const double DefaultCapacity = 400;

    /// <summary>Blocks of store spent per block of net climb, on top of the distance itself.</summary>
    public const double RiseSurcharge = 2.0;

    /// <summary>Blocks of store credited back per block of net descent. Gravity does some of the work.</summary>
    public const double DropCredit = 1.0;

    /// <summary>
    /// A descending trip never costs less than this fraction of its length. Without it a steep enough
    /// downhill line is free forever, and a machine that costs nothing to run stops being a machine.
    /// </summary>
    public const double MinCostFraction = 0.25;

    /// <summary>
    /// Load a tower puts on its mechanical network while it is winding. Sits between the quern's 0.1 and
    /// the helve-hammer toggle's 0.125, so a ropeway reads as a serious machine, and a maxed wood windmill
    /// (stall budget 0.75) still drives several of them.
    /// </summary>
    public const float WindingResistance = 0.12f;

    /// <summary>
    /// Load once the store is full, or when there is no store to wind. The pulverizer's idle/loaded shape
    /// (0.005 / 0.085): a finished ropeway must not permanently tax the mill it shares a line with.
    /// </summary>
    public const float IdleResistance = 0.005f;

    /// <summary>
    /// One tower's contribution over <paramref name="dt"/> seconds, clamped to capacity. POOLING is this
    /// function being called once per powered tower against the same store: the rate is linear in speed,
    /// so two towers at 0.3 bank exactly what one at 0.6 does and neither has to know about the other.
    /// That is the whole of "contributions pool" - there is no coordination and nothing to keep in sync,
    /// which is also why a tower in an unloaded chunk simply stops contributing instead of breaking it.
    /// </summary>
    public static double Wind(double stored, double capacity, double trueSpeed, double dt)
    {
        if (double.IsNaN(stored) || stored < 0) stored = 0;
        if (double.IsNaN(capacity) || capacity <= 0) return 0;
        if (double.IsNaN(trueSpeed) || double.IsNaN(dt) || trueSpeed <= 0 || dt <= 0) return Math.Min(capacity, stored);

        return Math.Min(capacity, stored + trueSpeed * ChargePerSpeedSecond * dt);
    }

    /// <summary>
    /// What a trip of <paramref name="length"/> blocks that ends <paramref name="netClimb"/> blocks higher
    /// (negative for lower) costs the store. Quoted and paid in full at departure, which is what makes a
    /// started journey finish no matter what the wind or the chunk loader does thirty seconds later.
    /// <para>
    /// Siting matters in one line of arithmetic: a 100-block line is 100 level, 180 climbing 40, and 60
    /// descending 40. A downhill ore line being cheap to run is the historically correct outcome.
    /// </para>
    /// </summary>
    public static double Quote(double length, double netClimb)
    {
        if (double.IsNaN(length) || length <= 0) return 0;
        if (double.IsNaN(netClimb)) netClimb = 0;

        var cost = length + (netClimb > 0 ? RiseSurcharge * netClimb : DropCredit * netClimb);
        return Math.Max(MinCostFraction * length, cost);
    }

    /// <summary>
    /// Whether the store can pay a quote outright. Fails closed on NaN - a trip nobody can price is a trip
    /// that does not leave, which is the only failure mode here that cannot strand anybody.
    /// </summary>
    public static bool CanAfford(double stored, double cost)
    {
        if (double.IsNaN(cost)) return false;
        if (cost <= 0) return true;
        return !double.IsNaN(stored) && stored >= cost;
    }
}
