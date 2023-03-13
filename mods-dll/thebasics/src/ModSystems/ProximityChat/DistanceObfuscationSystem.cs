using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

public class DistanceObfuscationSystem : BaseSubSystem
{
    private Random _random;

    public DistanceObfuscationSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system,
        api, config)
    {
        _random = new Random();
    }

    public string ObfuscateMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string message,
        ProximityChatMode? tempMode = null)
    {
        if (!Config.EnableDistanceObfuscationSystem)
        {
            return message;
        }
        
        var distance = sendingPlayer.Entity.ServerPos.DistanceTo(receivingPlayer.Entity.ServerPos);

        var chatMode = sendingPlayer.GetChatMode(tempMode);
        var obfuscationRange = Config.ProximityChatModeObfuscationRanges[chatMode];
        var maxRange = Config.ProximityChatModeDistances[chatMode];

        if (distance < obfuscationRange)
        {
            return message;
        }

        var percentage = (distance - obfuscationRange) / (maxRange - obfuscationRange);

        return message.Select(character =>
        {
            if (ChatHelper.IsPunctuation(character) || ChatHelper.IsDelimiter(character))
            {
                return character;
            }
            return _random.NextDouble() < percentage ? '*' : character;
        }).ToString();
    }
}