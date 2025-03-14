using ProtoBuf;
using System.Runtime.Serialization;
namespace thebasics.ModSystems.ProximityChat.Models
{
    [ProtoContract]
    public enum ProximityChatMode
    {
        [EnumMember]
        [ProtoEnum]
        Normal,
        [EnumMember]
        [ProtoEnum]
        Whisper,
        [EnumMember]
        [ProtoEnum]
        Yell
    }
}