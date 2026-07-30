using System.Runtime.Serialization;
using ProtoBuf;

namespace thebasics.ModSystems.ProximityChat.Models
{
    /// <summary>
    /// The sticky "kind" a player's plain chat lines are treated as, independent of
    /// <see cref="ProximityChatMode"/>, which controls delivery range.
    ///
    /// The two axes are deliberately separate: OOC and emotes are already delivered at the player's
    /// range mode, so whispered OOC and yelled emotes are both meaningful. Explicit message prefixes
    /// still override this for a single line.
    /// </summary>
    [ProtoContract]
    public enum ChatOverrideMode
    {
        /// <summary>Plain lines are in-character speech.</summary>
        [EnumMember]
        [ProtoEnum]
        None,

        [EnumMember]
        [ProtoEnum]
        Emote,

        [EnumMember]
        [ProtoEnum]
        Ooc,

        /// <summary>Global OOC ignores the range axis entirely; it always reaches every online player.</summary>
        [EnumMember]
        [ProtoEnum]
        GlobalOoc
    }
}
