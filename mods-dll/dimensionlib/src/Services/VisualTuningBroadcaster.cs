using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DimensionLib.Api;
using DimensionLib.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class VisualTuningBroadcaster
{
    private readonly ICoreServerAPI _api;
    private readonly IServerNetworkChannel _serverChannel;

    public VisualTuningBroadcaster(ICoreServerAPI api, IServerNetworkChannel serverChannel)
    {
        _api = api;
        _serverChannel = serverChannel;
    }

    public DimensionLibResult Send(IServerPlayer player, string raw)
    {
        var recipients = player != null
            ? new[] { player }
            : _api.World.AllOnlinePlayers.OfType<IServerPlayer>().ToArray();

        if (recipients.Length == 0)
        {
            return DimensionLibResult.Fail("No online players are available for visual tuning.", "missing-visual-tuning-recipient");
        }

        var cmdArgs = new CmdArgs(raw ?? string.Empty);
        var action = cmdArgs.PopWord(string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action))
        {
            return DimensionLibResult.Fail("Usage: /dlib visual status | reset | preset <clear|thin|default> | set <key> <value>", "missing-visual-action");
        }

        if (action == "status")
        {
            SendMessage(new DimensionVisualTuningMessage { Status = true }, recipients);
            return DimensionLibResult.Ok($"Requested DimensionLib visual status from {recipients.Length} client(s). Check client-main.log for the state snapshot.");
        }

        if (action == "reset")
        {
            SendMessage(new DimensionVisualTuningMessage { Reset = true }, recipients);
            return DimensionLibResult.Ok($"Reset DimensionLib visual tuning on {recipients.Length} client(s).");
        }

        if (action == "preset")
        {
            var presetId = cmdArgs.PopWord(string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return DimensionLibResult.Fail("Usage: /dlib visual preset <clear|thin|default>", "missing-visual-preset");
            }

            SendMessage(new DimensionVisualTuningMessage { PresetId = presetId }, recipients);
            return DimensionLibResult.Ok($"Sent DimensionLib visual preset '{presetId}' to {recipients.Length} client(s).");
        }

        if (action != "set")
        {
            return DimensionLibResult.Fail("Usage: /dlib visual status | reset | preset <clear|thin|default> | set <key> <value>", "unknown-visual-action");
        }

        var key = cmdArgs.PopWord(string.Empty).Trim();
        var valueText = cmdArgs.PopWord(string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(valueText))
        {
            return DimensionLibResult.Fail("Usage: /dlib visual set <key> <value>", "missing-visual-key-value");
        }

        if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return DimensionLibResult.Fail($"'{valueText}' is not a valid invariant-culture float.", "invalid-visual-value");
        }

        SendMessage(new DimensionVisualTuningMessage { Key = key, Value = value }, recipients);
        return DimensionLibResult.Ok($"Sent DimensionLib visual tuning {key}={value.ToString(CultureInfo.InvariantCulture)} to {recipients.Length} client(s).");
    }

    private void SendMessage(DimensionVisualTuningMessage message, IEnumerable<IServerPlayer> recipients)
    {
        foreach (var recipient in recipients)
        {
            _serverChannel?.SendPacket(message, recipient);
        }
    }
}
