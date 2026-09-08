using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace thebasics.ModSystems.DiceRolling;

// The player overload audits raw arguments before dispatch. Scope this replacement to exact
// owned command objects, including paths reached through aliases of the namespaced root.
internal sealed class PrivateDiceCommandPatch : IDisposable
{
    private static readonly Dictionary<IChatCommandApi, PrivateDiceCommandPatch> Active = new();
    private readonly IChatCommandApi api;
    private readonly HashSet<IChatCommand> owned = new(ReferenceEqualityComparer.Instance);
    private readonly Harmony harmony;
    internal static MethodInfo AuditedExecute => AccessTools.Method(typeof(ChatCommandApi), "Execute",
        new[] { typeof(string), typeof(IServerPlayer), typeof(int), typeof(string), typeof(Action<TextCommandResult>) });

    internal PrivateDiceCommandPatch(IChatCommandApi api)
    {
        this.api = api;
        harmony = new Harmony("thebasics.private-dice." + Guid.NewGuid().ToString("N"));
        harmony.Patch(AuditedExecute ?? throw new InvalidOperationException("Private dice audit boundary unavailable."),
            prefix: new HarmonyMethod(typeof(PrivateDiceCommandPatch), nameof(Prefix)));
        Active.Add(api, this);
    }
    internal void Own(IChatCommand command) => owned.Add(command);

    internal bool IsPrivate(string commandName, string arguments)
    {
        var command = api.Get(commandName?.ToLowerInvariant() ?? "");
        var remaining = new CmdArgs(arguments ?? "");
        while (command != null)
        {
            if (owned.Contains(command)) return true;
            var word = remaining.PopWord()?.ToLowerInvariant();
            if (word == null || !command.AllSubcommands.TryGetValue(word, out command)) return false;
        }
        return false;
    }

    private static bool Prefix(ChatCommandApi __instance, string commandName, IServerPlayer player, int groupId,
        string args, Action<TextCommandResult> onCommandComplete)
    {
        if (!Active.TryGetValue(__instance, out var patch) || !patch.IsPrivate(commandName, args)) return true;
        try
        {
            __instance.Execute(commandName, new TextCommandCallingArgs
            {
                Caller = new Caller { Player = player, Pos = player.Entity?.Pos.XYZ, FromChatGroupId = groupId },
                RawArgs = new CmdArgs(args ?? "")
            }, result =>
            {
                if (!string.IsNullOrEmpty(result.StatusMessage))
                    player.SendMessage(groupId, result.StatusMessage,
                        result.Status == EnumCommandStatus.Success ? EnumChatType.CommandSuccess : EnumChatType.CommandError);
                onCommandComplete?.Invoke(result);
            });
        }
        catch (Exception)
        {
            // Fail closed: never fall back into the audited overload, even for parser/callback errors.
            try { player.SendMessage(groupId, "The private roll could not be completed.", EnumChatType.CommandError); }
            catch (Exception) { /* A disconnected player cannot receive the fixed error. */ }
        }
        return false;
    }

    public void Dispose()
    {
        Active.Remove(api);
        harmony.Unpatch(AuditedExecute, HarmonyPatchType.Prefix, harmony.Id);
        owned.Clear();
    }
}
