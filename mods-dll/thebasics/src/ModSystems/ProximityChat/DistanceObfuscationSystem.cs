using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat;

public class DistanceObfuscationSystem : BaseSubSystem
{
    private readonly Random _random;

    public DistanceObfuscationSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system,
        api, config)
    {
        _random = new Random();
    }

    public void ObfuscateMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, ref string message,
        ProximityChatMode? tempMode = null)
    {
        if (!Config.EnableDistanceObfuscationSystem)
        {
            return;
        }

        var distance = sendingPlayer.GetDistance(receivingPlayer);
        var chatMode = sendingPlayer.GetChatMode(tempMode);
        var obfuscationRange = Config.ProximityChatModeObfuscationRanges[chatMode];
        var maxRange = Config.ProximityChatModeDistances[chatMode];

        if (distance < obfuscationRange)
        {
            return;
        }

        var percentage = (distance - obfuscationRange) / (maxRange - obfuscationRange);

        message = string.Join("", message.Select(character =>
        {
            if (ChatHelper.IsPunctuation(character) || ChatHelper.IsWhitespace(character))
            {
                return character;
            }

            return _random.NextDouble() < percentage ? '*' : character;
        }));
    }
}