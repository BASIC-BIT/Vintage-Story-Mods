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

    private double GetDistance(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer) =>
        sendingPlayer.Entity.ServerPos.DistanceTo(receivingPlayer.Entity.ServerPos);

    public void ObfuscateMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, ref string message,
        ProximityChatMode? tempMode = null)
    {
        if (!Config.EnableDistanceObfuscationSystem)
        {
            return;
        }

        var distance = GetDistance(sendingPlayer, receivingPlayer);

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
            if (ChatHelper.IsPunctuation(character) || ChatHelper.IsDelimiter(character))
            {
                return character;
            }

            return _random.NextDouble() < percentage ? '*' : character;
        }));
    }

    public int GetFontSize(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer,
        ProximityChatMode? tempMode = null)
    {
        // Doesn't check if the system is disabled, that's up to the consumer

        var distance = GetDistance(sendingPlayer, receivingPlayer);
        var chatMode = sendingPlayer.GetChatMode(tempMode);
        var maxRange = Config.ProximityChatModeDistances[chatMode];
        var defaultSize = Config.ProximityChatDefaultFontSize[chatMode];
        
        var size = ((defaultSize - Config.ProximityChatMinimumFontSize) * (1.0d - (distance / maxRange))) + Config.ProximityChatMinimumFontSize;

        return (int) Math.Round(size);
    }
}