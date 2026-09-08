using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.DiceRolling;

internal sealed class DiceRollCommands : IDisposable
{
    internal const string Help = "Examples: /roll d20; /r 2d6+3 # forcing the gate; /proll 4d6kh3; /r 5d10!>=8. "
        + "Use /thebasics roll or /thebasics proll if a short command belongs to another mod. "
        + "Syntax: dN or NdN; + - * / and parentheses; floor(...), ceil(...), round(...) (halves away from zero). "
        + "Modifiers: ! explodes maximum faces; r<3 or ro<3 rerolls each matching die once; rr<3 repeats. "
        + "kh3/kl3 keep highest/lowest 3; dh1/dl1 drop highest/lowest 1; >=8 counts successes (also > < <= =). "
        + "Fixed order: explode, reroll, keep/drop, success count. Use # or // before an ambiguous reason. "
        + "Private aliases: /proll and /privateroll. "
        + "Limits: 512 input characters, 256 tokens, 32 nesting levels, 100 generated dice, 1,000,000 sides, "
        + "2,048 evaluation steps, 4,096 breakdown characters and 1,900 characters in the complete rendered result; 5 attempts per 10 seconds. Private rolls are self-only.";
    private readonly RPProximityChatSystem system;
    private readonly System.Func<string, DiceRollResult> evaluate;
    private readonly DiceAttemptGuard guard = new();
    private PrivateDiceCommandPatch privatePatch;

    internal DiceRollCommands(RPProximityChatSystem system, System.Func<string, DiceRollResult> evaluate = null)
    {
        this.system = system;
        this.evaluate = evaluate ?? (input => DiceEvaluator.EvaluateInput(input));
    }

    internal void Register()
    {
        // Install first: if the audit seam changes, no private command is exposed.
        privatePatch = new PrivateDiceCommandPatch(system.API.ChatCommands);
        var root = system.API.ChatCommands.GetOrCreate("thebasics");
        RegisterCommand(root.BeginSubCommand("roll"), false);
        RegisterCommand(root.BeginSubCommand("proll"), true);
        foreach (var alias in new[] { "roll", "r", "proll", "privateroll" })
        {
            if (system.API.ChatCommands.Get(alias) != null) continue;
            RegisterCommand(system.API.ChatCommands.Create(alias), alias is "proll" or "privateroll");
        }
        system.API.Event.PlayerDisconnect += OnDisconnect;
    }

    private void RegisterCommand(IChatCommand command, bool isPrivate)
    {
        command.RequiresPrivilege(Privilege.chat).RequiresPlayer().WithArgs(system.API.ChatCommands.Parsers.Unparsed("dice expression and optional reason")).WithDescription(isPrivate ? "Roll dice privately." : "Roll dice for your scene.")
            .HandleWith(args => Handle(args, isPrivate));
        if (isPrivate) privatePatch.Own(command);
    }

    internal TextCommandResult Handle(TextCommandCallingArgs args, bool isPrivate)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player == null) return TextCommandResult.Error("This command requires a player.");
        if (!guard.TryAcquire(player.PlayerUID, Environment.TickCount64)) return Outcome(isPrivate, "rate_limited", "Too many roll attempts. Wait a few seconds.");
        if (!system.Config.EnableDiceRolling) return Outcome(isPrivate, "disabled", "Dice rolling is disabled on this server.");
        var input = args.RawArgs?.PopAll() ?? "";
        if (string.IsNullOrWhiteSpace(input)) return Outcome(isPrivate, "help", Help);
        try
        {
            var mode = player.GetChatMode();
            var result = evaluate(input);
            if (isPrivate)
            {
                player.SendMessage(args.Caller.FromChatGroupId, DicePresentation.Chat(result, "", mode, true), EnumChatType.OwnMessage);
            }
            else
            {
                DeliverPublic(player, result, mode);
            }
            DiceAnalytics.RecordResult(result, mode, isPrivate);
            return Outcome(isPrivate, "success", null);
        }
        catch (DiceRollException ex) { return Outcome(isPrivate, "invalid_input", ex.Message); }
        // Never include raw input or exception text in logging, analytics or command framework errors.
        catch (Exception) { return Outcome(isPrivate, "failure", "The roll could not be completed. Please try again."); }
    }

    private void DeliverPublic(IServerPlayer player, DiceRollResult result, ProximityChatMode mode)
    {
        var names = new NameTransformer(system);
        var name = names.GetFormattedName(player, true, system.Config);
        var text = DicePresentation.Chat(result, name, mode, false);
        var range = system.Config.GetModeDistance(mode);
        var recipients = system.API.World.AllOnlinePlayers.OfType<IServerPlayer>()
            .Where(candidate => InRange(player, candidate, range)).ToList();
        if (!recipients.Contains(player)) recipients.Add(player);
        var context = new MessageContext { SendingPlayer = player, GroupId = system.ProximityChatId, Message = text, Recipients = recipients };
        context.SetFlag(MessageContext.IS_ROLL);
        context.SetFlag(MessageContext.IS_FROM_COMMAND);
        context.SetMetadata(MessageContext.CHAT_MODE, mode);
        context.SetMetadata(MessageContext.FORMATTED_NAME, name);
        var bubble = DicePresentation.Bubble(result, player, mode, system.Config, false);
        foreach (var recipient in recipients)
        {
            var recipientName = names.GetFormattedName(player, true, system.Config, recipient);
            recipient.SendMessage(system.ProximityChatId, DicePresentation.Chat(result, recipientName, mode, false), EnumChatType.OthersMessage, bubble);
        }
        system.API.Logger.Chat(text);
        system.RecordChatHistory(context, text);
        system.PublishProximityChatMessageProcessed(context, text);
    }

    internal static bool InRange(IServerPlayer sender, IServerPlayer recipient, int range)
    {
        var origin = sender.Entity?.Pos;
        var target = recipient.Entity?.Pos;
        return origin != null && target != null && origin.Dimension == target.Dimension
            && (ModConfig.IsUnlimitedRange(range) || target.AsBlockPos.ManhattanDistance(origin.AsBlockPos) < range);
    }

    private static TextCommandResult Outcome(bool isPrivate, string outcome, string message)
    {
        var success = outcome is "success" or "help";
        AnalyticsService.TrackCommandUsed(isPrivate ? "proll" : "roll", success, outcome);
        return success ? TextCommandResult.Success(message) : TextCommandResult.Error(message);
    }
    private void OnDisconnect(IServerPlayer player) => guard.Remove(player.PlayerUID);
    public void Dispose()
    {
        system.API.Event.PlayerDisconnect -= OnDisconnect;
        privatePatch?.Dispose();
        guard.Clear();
    }
}

internal sealed class DiceAttemptGuard
{
    private readonly Dictionary<string, Queue<long>> attempts = new(StringComparer.Ordinal);
    internal bool TryAcquire(string uid, long now)
    {
        if (!attempts.TryGetValue(uid, out var queue)) attempts[uid] = queue = new Queue<long>();
        while (queue.Count > 0 && now - queue.Peek() >= 10000) queue.Dequeue();
        if (queue.Count >= 5) return false;
        queue.Enqueue(now);
        return true;
    }
    internal void Remove(string uid) => attempts.Remove(uid);
    internal void Clear() => attempts.Clear();
}
