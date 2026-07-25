using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

public sealed class BEFlywheelPart : BlockEntity
{
    public BlockPos Principal { get; set; }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        ReadPrincipal(tree);
    }

    internal void ReadPrincipal(ITreeAttribute tree)
    {
        bool hasPrincipal = tree.GetBool("cp", tree.HasAttribute("cx"));
        Principal = hasPrincipal
            ? new BlockPos(tree.GetInt("cx"), tree.GetInt("cy"), tree.GetInt("cz"), tree.GetInt("cd", 0))
            : null;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        WritePrincipal(tree);
    }

    internal void WritePrincipal(ITreeAttribute tree)
    {
        tree.SetBool("cp", Principal != null);
        tree.SetInt("cx", Principal?.X ?? -1);
        tree.SetInt("cy", Principal?.Y ?? -1);
        tree.SetInt("cz", Principal?.Z ?? -1);
        tree.SetInt("cd", Principal?.dimension ?? 0);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        if (Principal == null)
        {
            return;
        }

        Api.World.BlockAccessor.GetBlockEntity(Principal)?.GetBlockInfo(forPlayer, sb);
    }
}
