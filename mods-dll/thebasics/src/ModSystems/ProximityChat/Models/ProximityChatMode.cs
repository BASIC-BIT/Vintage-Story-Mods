using System.Runtime.Serialization;
using ProtoBuf;
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
