using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace PocketDimensions;

public sealed class PocketWaystoneBlockEntity : BlockEntity
{
    private const string ModId = "pocketdimensions";

    public string EndpointId { get; private set; }

    public string BoundDimensionId { get; private set; }

    public string ReturnAnchorDimensionId { get; private set; }

    public string BoundByPlayerUid { get; private set; }

    public string BoundByPlayerName { get; private set; }

    public bool IsBound => !string.IsNullOrWhiteSpace(BoundDimensionId);

    public bool IsReturnAnchor => !string.IsNullOrWhiteSpace(ReturnAnchorDimensionId);

    public string EnsureEndpointId()
    {
        if (string.IsNullOrWhiteSpace(EndpointId))
        {
            EndpointId = Guid.NewGuid().ToString("N");
            MarkDirty();
        }

        return EndpointId;
    }

    public void BindTo(string dimensionId, IServerPlayer player)
    {
        EnsureEndpointId();
        BoundDimensionId = dimensionId?.Trim();
        ReturnAnchorDimensionId = null;
        BoundByPlayerUid = player?.PlayerUID;
        BoundByPlayerName = player?.PlayerName;
        MarkDirty();
    }

    public void MarkAsReturnAnchor(string dimensionId)
    {
        ReturnAnchorDimensionId = dimensionId?.Trim();
        BoundDimensionId = null;
        BoundByPlayerUid = null;
        BoundByPlayerName = null;
        MarkDirty();
    }

    public void ClearBinding()
    {
        BoundDimensionId = null;
        ReturnAnchorDimensionId = null;
        BoundByPlayerUid = null;
        BoundByPlayerName = null;
        MarkDirty();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("endpointId", EndpointId ?? string.Empty);
        tree.SetString("boundDimensionId", BoundDimensionId ?? string.Empty);
        tree.SetString("returnAnchorDimensionId", ReturnAnchorDimensionId ?? string.Empty);
        tree.SetString("boundByPlayerUid", BoundByPlayerUid ?? string.Empty);
        tree.SetString("boundByPlayerName", BoundByPlayerName ?? string.Empty);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        EndpointId = EmptyToNull(tree.GetString("endpointId", string.Empty));
        BoundDimensionId = EmptyToNull(tree.GetString("boundDimensionId", string.Empty));
        ReturnAnchorDimensionId = EmptyToNull(tree.GetString("returnAnchorDimensionId", string.Empty));
        BoundByPlayerUid = EmptyToNull(tree.GetString("boundByPlayerUid", string.Empty));
        BoundByPlayerName = EmptyToNull(tree.GetString("boundByPlayerName", string.Empty));
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (IsReturnAnchor)
        {
            dsc.AppendLine($"Legacy pocket return anchor: {ShortName(ReturnAnchorDimensionId)}");
            dsc.AppendLine("Use the generated Pocket Return Pedestal instead.");
        }
        else if (IsBound)
        {
            dsc.AppendLine($"Bound pocket: {ShortName(BoundDimensionId)}");
            if (!string.IsNullOrWhiteSpace(BoundByPlayerName))
            {
                dsc.AppendLine($"Bound by: {BoundByPlayerName}");
            }
        }
        else
        {
            dsc.AppendLine("Unbound. Use /pocket bind <name> while looking at this Waystone.");
        }
    }

    private static string EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ShortName(string dimensionId)
    {
        return dimensionId != null && dimensionId.StartsWith(ModId + ":", StringComparison.Ordinal)
            ? dimensionId.Substring(ModId.Length + 1)
            : dimensionId;
    }
}
