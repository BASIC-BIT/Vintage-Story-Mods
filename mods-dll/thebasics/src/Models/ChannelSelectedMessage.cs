using ProtoBuf;
namespace thebasics.Models
{
    [ProtoContract]
    public class ChannelSelectedMessage
    {
        [ProtoMember(1)]
        public int? GroupId;
    }
}