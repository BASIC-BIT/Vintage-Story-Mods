using System.Collections.Generic;
using ProtoBuf;

namespace PocketDimensions;

[ProtoContract]
public sealed class PocketElevatorTravelRequest
{
    [ProtoMember(1)]
    public int Direction { get; set; }
}

[ProtoContract]
public sealed class PocketLayerCreationPrompt
{
    [ProtoMember(1)]
    public int Direction { get; set; }

    [ProtoMember(2)]
    public string StackName { get; set; }

    [ProtoMember(3)]
    public string StackId { get; set; }

    [ProtoMember(4)]
    public int SourceLayerIndex { get; set; }

    [ProtoMember(5)]
    public int TargetLayerIndex { get; set; }
}

[ProtoContract]
public sealed class PocketLayerCreationResponse
{
    [ProtoMember(1)]
    public int Direction { get; set; }

    [ProtoMember(2)]
    public bool Create { get; set; }

    [ProtoMember(3)]
    public string StackId { get; set; }

    [ProtoMember(4)]
    public int SourceLayerIndex { get; set; }

    [ProtoMember(5)]
    public int TargetLayerIndex { get; set; }
}

[ProtoContract]
public sealed class PocketElevatorPlacementPrompt
{
    [ProtoMember(1)]
    public int Direction { get; set; }

    [ProtoMember(2)]
    public string StackName { get; set; }

    [ProtoMember(3)]
    public string StackId { get; set; }

    [ProtoMember(4)]
    public int SourceLayerIndex { get; set; }

    [ProtoMember(5)]
    public int TargetLayerIndex { get; set; }
}

[ProtoContract]
public sealed class PocketElevatorPlacementResponse
{
    [ProtoMember(1)]
    public int Direction { get; set; }

    [ProtoMember(2)]
    public bool Place { get; set; }

    [ProtoMember(3)]
    public string StackId { get; set; }

    [ProtoMember(4)]
    public int SourceLayerIndex { get; set; }

    [ProtoMember(5)]
    public int TargetLayerIndex { get; set; }
}

[ProtoContract]
public sealed class PocketHudStateMessage
{
    [ProtoMember(1)]
    public bool InPocket { get; set; }

    [ProtoMember(2)]
    public string PocketName { get; set; }

    [ProtoMember(3)]
    public int LayerIndex { get; set; }

    [ProtoMember(4)]
    public int LocalX { get; set; }

    [ProtoMember(5)]
    public int LocalY { get; set; }

    [ProtoMember(6)]
    public int LocalZ { get; set; }
}

[ProtoContract]
public sealed class PocketDirectoryRequest
{
    [ProtoMember(1)]
    public bool Refresh { get; set; }
}

[ProtoContract]
public sealed class PocketDirectoryActionRequest
{
    [ProtoMember(1)]
    public string Action { get; set; }

    [ProtoMember(2)]
    public string DimensionId { get; set; }

    [ProtoMember(3)]
    public string DisplayName { get; set; }

    [ProtoMember(4)]
    public string Slug { get; set; }

    [ProtoMember(5)]
    public int SizeChunks { get; set; }

    [ProtoMember(6)]
    public int SpawnY { get; set; }

    [ProtoMember(7)]
    public string StackId { get; set; }

    [ProtoMember(8)]
    public int LayerIndex { get; set; }
}

[ProtoContract]
public sealed class PocketDirectoryStateMessage
{
    [ProtoMember(1)]
    public List<PocketDirectoryStackMessage> Stacks { get; set; } = new List<PocketDirectoryStackMessage>();

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public bool Success { get; set; } = true;

    [ProtoMember(4)]
    public bool CanCreatePocket { get; set; }

    [ProtoMember(5)]
    public bool CanCreateLayer { get; set; }

    [ProtoMember(6)]
    public string SelectedStackId { get; set; }

    [ProtoMember(7)]
    public string CurrentLocationText { get; set; }

    [ProtoMember(8)]
    public int DefaultSizeChunks { get; set; }

    [ProtoMember(9)]
    public int DefaultSpawnY { get; set; }
}

[ProtoContract]
public sealed class PocketDirectoryStackMessage
{
    [ProtoMember(1)]
    public string StackId { get; set; }

    [ProtoMember(2)]
    public string DisplayName { get; set; }

    [ProtoMember(3)]
    public string OwnerPlayerUid { get; set; }

    [ProtoMember(4)]
    public string OwnerPlayerName { get; set; }

    [ProtoMember(5)]
    public bool IsOwner { get; set; }

    [ProtoMember(6)]
    public bool CanCreateLayer { get; set; }

    [ProtoMember(7)]
    public List<PocketDirectoryLayerMessage> Layers { get; set; } = new List<PocketDirectoryLayerMessage>();
}

[ProtoContract]
public sealed class PocketDirectoryLayerMessage
{
    [ProtoMember(1)]
    public int Index { get; set; }

    [ProtoMember(2)]
    public string DimensionId { get; set; }

    [ProtoMember(3)]
    public string DisplayName { get; set; }

    [ProtoMember(4)]
    public bool Prepared { get; set; }

    [ProtoMember(5)]
    public bool Orphaned { get; set; }

    [ProtoMember(6)]
    public bool CanTeleport { get; set; }

    [ProtoMember(7)]
    public bool IsCurrent { get; set; }

    [ProtoMember(8)]
    public bool CanEdit { get; set; }
}
