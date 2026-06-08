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
