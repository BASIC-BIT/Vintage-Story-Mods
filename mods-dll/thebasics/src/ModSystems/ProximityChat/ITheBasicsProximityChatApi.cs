using System;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat;

public interface ITheBasicsProximityChatApi
{
    /// <summary>
    /// Fired once for a proximity message that The BASICs accepted, logged, and delivered
    /// to at least one immediate or delayed recipient.
    /// </summary>
    event EventHandler<ProximityChatMessageEventArgs> ProximityChatMessageProcessed;

    /// <summary>
    /// The chat group The BASICs currently uses for proximity chat.
    /// </summary>
    int ProximityChatGroupId { get; }
}
