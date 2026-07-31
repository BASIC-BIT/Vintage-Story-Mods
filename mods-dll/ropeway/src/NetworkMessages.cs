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
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerCandidatesResponse
{
    public BlockPos FromTower;
    public int RopeInInventory;
    public List<TowerCandidate> Candidates = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TowerLinkRequest
{
    public BlockPos FromTower;
    public BlockPos ToTower;
}
