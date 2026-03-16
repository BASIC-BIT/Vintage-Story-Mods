using ProtoBuf;

namespace thebasics.Models;

/// <summary>
/// Server → client packet for an environmental message placed at a specific world position
/// (via raycast from the sender's look direction).
/// </summary>
[ProtoContract]
public class PlacedEnvironmentMessage
{
    /// <summary>World X coordinate of the bubble position.</summary>
    [ProtoMember(1)]
    public double X { get; set; }

    /// <summary>World Y coordinate of the bubble position.</summary>
    [ProtoMember(2)]
    public double Y { get; set; }

    /// <summary>World Z coordinate of the bubble position.</summary>
    [ProtoMember(3)]
    public double Z { get; set; }

    /// <summary>The VTML-formatted bubble text to render.</summary>
    [ProtoMember(4)]
    public string BubbleText { get; set; }
}
