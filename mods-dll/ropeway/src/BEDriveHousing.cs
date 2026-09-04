using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent.Mechanics;

namespace Ropeway;

/// <summary>
/// The drive station's mechanical intake, at the foot of its machine leg: it carries the <c>MPConsumer</c>
/// and nothing else. Which line it drives is not its question - it is cell [3,0,0] of exactly one station,
/// and <see cref="BEPylonBase.Intake"/> is what looks it up, from the footing, at a known offset.
/// <para>
/// It used to answer that question itself, at lookup time, from the nearest footing on a line inside an
/// eight-block sphere - and the accessor, the acceptance predicate, the tie-break, the position table it
/// registered itself in, the load-declaring tick listener and the placement refusal that kept it near a
/// tower were all parts of answering it. Membership of a structure answers it exactly, so all of that is
/// gone and what remains is a block that holds a vanilla behaviour.
/// </para>
/// </summary>
public class BEDriveHousing : BlockEntity
{
    /// <summary>Vanilla registers <c>MPConsumer</c> itself, so the mod registers nothing for it.</summary>
    private BEBehaviorMPConsumer mpc;

    /// <summary>
    /// The vanilla consumer behaviour, or null before <c>Initialize</c>. The station that owns this cell
    /// reads its speed and writes its resistance; nothing else touches it. Exposed as the handle rather than
    /// as a speed property and a load method, because two accessors onto one object is two things to keep
    /// in step with it.
    /// </summary>
    public BEBehaviorMPConsumer Consumer => mpc;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        mpc = GetBehavior<BEBehaviorMPConsumer>();
    }

    /// <summary>
    /// Deliberately discarding what base returns, and this is load bearing rather than sloppy:
    /// <c>BEBehaviorMPConsumer.OnTesselation</c> returns TRUE for any non-null block, because a vanilla
    /// consumer expects the instanced <c>MechNetworkRenderer</c> to draw it instead. With
    /// <c>mechPartShape</c> null nothing draws it, so honouring that true would make the housing vanish.
    /// </summary>
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        base.OnTesselation(mesher, tessThreadTesselator);
        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        // No line named here any more, and that is the point rather than a loss. This panel used to have to
        // say WHICH line the housing had decided to drive, because two lines passing within eight blocks of
        // it both looked like candidates and only one of them was getting the power. A cell of a station
        // drives the station's line, so the sentence has nothing left to disambiguate.
        dsc.AppendLine(Lang.Get("ropeway:housing-what"));

        dsc.AppendLine(mpc?.Network == null
            ? Lang.Get("ropeway:blockinfo-nodrive")
            : Lang.Get("ropeway:blockinfo-drive", Math.Round(mpc.TrueSpeed, 2)));
    }
}
