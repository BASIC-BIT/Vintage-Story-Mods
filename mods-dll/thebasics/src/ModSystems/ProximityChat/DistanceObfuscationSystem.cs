using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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
        ProximityChatMode? tempMode = null, int occlusionPenalty = 0)
    {
        if (!Config.EnableDistanceObfuscationSystem)
        {
            return;
        }

        var chatMode = sendingPlayer.GetChatMode(tempMode);
        var obfuscationRange = Config.GetModeObfuscationRange(chatMode);
        var maxRange = Config.GetModeDistance(chatMode);

        // Unlimited range has no far edge to fade toward, so there is nothing to obfuscate against.
        // Checked before reading positions so this never depends on both players having entities.
        if (ModConfig.IsUnlimitedRange(maxRange))
        {
            return;
        }

        // Walls between the two players read as extra distance, so speech degrades toward
        // unintelligible instead of cutting out at a hard boundary.
        var distance = sendingPlayer.GetDistance(receivingPlayer) + occlusionPenalty;
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
