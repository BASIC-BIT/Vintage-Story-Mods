using ProtoBuf;
using System.Runtime.Serialization;
namespace thebasics.ModSystems.PlayerStats.Models
{
    [ProtoContract]
    public enum PlayerStatType
    {
        [EnumMember]
        [ProtoEnum]
        Deaths,
        [EnumMember]
        [ProtoEnum]
        PlayerKills,
        [EnumMember]
        [ProtoEnum]
        NpcKills,
        [EnumMember]
        [ProtoEnum]
        BlockBreaks,
        [EnumMember]
        [ProtoEnum]
        DistanceTravelled,
    }
}