using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

internal sealed class Th3EssentialsDiscordRelay
{
    private const string Th3EssentialsSystemName = "Th3Essentials.Th3Essentials";
    private static readonly Regex EveryoneMentionRegex = new(@"@everyone", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HereMentionRegex = new(@"@here", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ICoreServerAPI _api;
    private bool _loggedUnavailable;
    private bool _loggedReflectionFailure;

    public Th3EssentialsDiscordRelay(ICoreServerAPI api)
    {
        _api = api;
    }

    public void Relay(ModConfig config, string renderedMessage)
    {
        if (config?.EnableTh3EssentialsDiscordRelay != true)
        {
            return;
        }

        var discordMessage = FormatRelayMessage(renderedMessage);
        if (string.IsNullOrWhiteSpace(discordMessage))
        {
            return;
        }

        try
        {
            if (!TryGetTh3Discord(out var discord))
            {
                return;
            }

            TryEnqueue(discord, discordMessage);
        }
        catch (Exception ex)
        {
            LogReflectionFailure($"unexpected error: {ex.GetType().Name}");
        }
    }

    internal static string FormatRelayMessage(string renderedMessage)
    {
        if (string.IsNullOrWhiteSpace(renderedMessage))
        {
            return string.Empty;
        }

        var plain = VtmlUtils.StripVtmlTags(renderedMessage);
        plain = VtmlUtils.UnescapeVtml(plain).Trim();
        plain = EveryoneMentionRegex.Replace(plain, "@_everyone");
        plain = HereMentionRegex.Replace(plain, "@_here");
        return plain;
    }

    internal bool TryEnqueue(object discord, string message)
    {
        if (discord == null || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (!IsDiscordChatRelayEnabled(discord) || !HasDiscordChannel(discord))
        {
            return false;
        }

        // Th3Essentials only exposes system/admin send helpers publicly. Its normal chat
        // queue is private, but using it preserves the configured chat channel and batching.
        var queueField = discord.GetType().GetField("sendQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        if (queueField?.GetValue(discord) is not ConcurrentQueue<string> queue)
        {
            LogReflectionFailure("Th3Essentials sendQueue field was not found");
            return false;
        }

        queue.Enqueue(message);
        return true;
    }

    private bool TryGetTh3Discord(out object discord)
    {
        discord = null;

        if (_api?.ModLoader == null)
        {
            return false;
        }

        if (!_api.ModLoader.IsModSystemEnabled(Th3EssentialsSystemName))
        {
            LogUnavailable("Th3Essentials mod system is not enabled");
            return false;
        }

        var th3Essentials = _api.ModLoader.GetModSystem(Th3EssentialsSystemName);
        if (th3Essentials == null)
        {
            return false;
        }

        var discordProperty = th3Essentials.GetType().GetProperty("Th3Discord", BindingFlags.Instance | BindingFlags.Public);
        discord = discordProperty?.GetValue(th3Essentials);
        return discord != null;
    }

    private static bool HasDiscordChannel(object discord)
    {
        var channelProperty = discord.GetType().GetProperty("DiscordChannel", BindingFlags.Instance | BindingFlags.Public);
        return channelProperty?.GetValue(discord) != null;
    }

    private bool IsDiscordChatRelayEnabled(object discord)
    {
        var configField = discord.GetType().GetField("Config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var config = configField?.GetValue(discord);
        if (config == null)
        {
            LogReflectionFailure("Th3Essentials Config field was not found");
            return false;
        }

        var relayField = config.GetType().GetField("DiscordChatRelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (relayField?.GetValue(config) is not bool enabled)
        {
            LogReflectionFailure("Th3Essentials DiscordChatRelay field was not found");
            return false;
        }

        return enabled;
    }

    private void LogUnavailable(string reason)
    {
        if (_loggedUnavailable)
        {
            return;
        }

        _loggedUnavailable = true;
        _api?.Logger.Warning($"[THEBASICS] Th3Essentials Discord relay is enabled in The BASICs, but relay is unavailable: {reason}.");
    }

    private void LogReflectionFailure(string reason)
    {
        if (_loggedReflectionFailure)
        {
            return;
        }

        _loggedReflectionFailure = true;
        _api?.Logger.Warning($"[THEBASICS] Could not relay proximity chat to Th3Essentials Discord: {reason}.");
    }
}
