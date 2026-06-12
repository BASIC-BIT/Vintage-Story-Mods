using System.Globalization;
using ProtoBuf;
using Vintagestory.API.Common.Entities;

namespace thebasics.ModSystems.HomeSpawn;

[ProtoContract]
public class HomeSpawnLocation
{
    [ProtoMember(1)]
    public double X { get; set; }

    [ProtoMember(2)]
    public double Y { get; set; }

    [ProtoMember(3)]
    public double Z { get; set; }

    [ProtoMember(4)]
    public float Yaw { get; set; }

    [ProtoMember(5)]
    public float Pitch { get; set; }

    [ProtoMember(6)]
    public float Roll { get; set; }

    [ProtoMember(7)]
    public int Dimension { get; set; }

    public static HomeSpawnLocation From(EntityPos pos)
    {
        return new HomeSpawnLocation
        {
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Yaw = pos.Yaw,
            Pitch = pos.Pitch,
            Roll = pos.Roll,
            Dimension = pos.Dimension
        };
    }

    public EntityPos ToEntityPos()
    {
        return new EntityPos(X, Y, Z, Yaw, Pitch, Roll)
        {
            Dimension = Dimension
        };
    }

    public bool IsSameDimensionAs(EntityPos pos)
    {
        return pos != null && Dimension == pos.Dimension;
    }

    public string Format()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#}, {1:0.#}, {2:0.#}", X, Y, Z);
    }
}
