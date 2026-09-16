using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace Ropeway;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerCandidate
{
    public BlockPos Pos;
    public int Distance;
    public int RopeCost;

    /// <summary>The tower's player-set name, or null. The picker falls back to the compass bearing.</summary>
    public string Name;

    /// <summary>
    /// True for a tower this one is already linked to. Those rows are in the same list as the candidates so
    /// the picker shows one ordered set of neighbours, but they act as unlink rather than link.
    /// </summary>
    public bool Linked;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerCandidatesResponse
{
    public BlockPos FromTower;
    public int RopeInInventory;

    /// <summary>Name of the tower the picker was opened on, so the rename field can be pre-filled.</summary>
    public string FromName;

    public List<TowerCandidate> Candidates = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerLinkRequest
{
    public BlockPos FromTower;
    public BlockPos ToTower;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerUnlinkRequest
{
    public BlockPos FromTower;
    public BlockPos ToTower;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerRenameRequest
{
    public BlockPos Tower;
    public string Name;
}

/// <summary>
/// A seated rider pressing the stop key. Unlike every other request in the mod this carries no position and
/// needs no reach check: the trust gate is the mount itself - the server takes the cabin from what the
/// sender is actually sitting in, and the id is only cross-checked against it, so a forged packet can move
/// nothing the sender is not already riding.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RiderStopRequest
{
    public long CabinEntityId;
}
