using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

public sealed class BEFlywheelPart : BlockEntity, IRotatable
{
    public BlockPos Principal { get; set; }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        ReadPrincipal(tree, Pos);
    }

    internal void ReadPrincipal(ITreeAttribute tree, BlockPos partPosition)
    {
        if (!tree.GetBool("cp") || !tree.GetBool("pr") || partPosition == null)
        {
            Principal = null;
            return;
        }

        Principal = partPosition.AddCopy(tree.GetInt("rx"), tree.GetInt("ry"), tree.GetInt("rz"));
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        WritePrincipal(tree, Pos);
    }

    internal void WritePrincipal(ITreeAttribute tree, BlockPos partPosition)
    {
        tree.SetBool("cp", Principal != null);
        bool canWriteRelative = Principal != null && partPosition != null;
        tree.SetBool("pr", canWriteRelative);
        tree.SetInt("rx", canWriteRelative ? Principal.X - partPosition.X : 0);
        tree.SetInt("ry", canWriteRelative ? Principal.Y - partPosition.Y : 0);
        tree.SetInt("rz", canWriteRelative ? Principal.Z - partPosition.Z : 0);
    }

    public void OnTransformed(
        IWorldAccessor worldAccessor,
        ITreeAttribute tree,
        int degreeRotation,
        Dictionary<int, AssetLocation> oldBlockIdMapping,
        Dictionary<int, AssetLocation> oldItemIdMapping,
        EnumAxis? flipAxis)
    {
        TransformRelativePrincipal(tree, degreeRotation, flipAxis);
    }

    internal static void TransformRelativePrincipal(ITreeAttribute tree, int degreeRotation, EnumAxis? flipAxis)
    {
        if (!tree.GetBool("pr"))
        {
            return;
        }

        int x = tree.GetInt("rx");
        int y = tree.GetInt("ry");
        int z = tree.GetInt("rz");

        switch (flipAxis)
        {
            case EnumAxis.X:
                x = -x;
                break;
            case EnumAxis.Y:
                y = -y;
                break;
            case EnumAxis.Z:
                z = -z;
                break;
        }

        (x, z) = degreeRotation switch
        {
            90 => (-z, x),
            180 => (-x, -z),
            270 => (z, -x),
            _ => (x, z)
        };

        tree.SetInt("rx", x);
        tree.SetInt("ry", y);
        tree.SetInt("rz", z);
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
