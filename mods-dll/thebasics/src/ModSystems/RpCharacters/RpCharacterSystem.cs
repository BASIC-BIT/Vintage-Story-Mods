using System.Linq;
using System.Collections.Generic;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.RpCharacters.Models;
using thebasics.Utilities;
using thebasics.Utilities.Parsers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterSystem : BaseBasicModSystem
{
    private readonly List<IRpCharacterSwitchParticipant> _externalParticipants = new List<IRpCharacterSwitchParticipant>();
    private RpCharacterService _characters;

    public bool RegisterSwitchParticipant(IRpCharacterSwitchParticipant participant)
    {
        if (participant == null || string.IsNullOrWhiteSpace(participant.Code))
        {
            return false;
        }

        _externalParticipants.RemoveAll(existing => existing.Code.Equals(participant.Code, System.StringComparison.OrdinalIgnoreCase));
        _externalParticipants.Add(participant);
        _characters?.RegisterParticipant(participant);
        return true;
    }

    protected override void BasicStartServerSide()
    {
        if (!Config.EnableRpCharacterSlots || !Config.EnableCharacterSheets)
        {
            return;
        }

        var participants = new List<IRpCharacterSwitchParticipant>();
        if (Config.EnableRpCharacterFullSwitching)
        {
            participants.Add(new RpCharacterSafetyParticipant(T));
            if (Config.EnableRpCharacterAppearanceSwitching)
            {
                participants.Add(new RpCharacterAppearanceParticipant());
            }

            if (Config.EnableRpCharacterInventorySwitching)
            {
                participants.Add(new RpCharacterInventoryParticipant());
            }

            if (Config.EnableRpCharacterBodySwitching)
            {
                participants.Add(new RpCharacterBodyParticipant());
            }
        }

        participants.AddRange(_externalParticipants);
        _characters = new RpCharacterService(Config, T, participants);
        HookEvents();
        RegisterCommands();
    }

    public override void Dispose()
    {
        if (API != null && _characters != null)
        {
            API.Event.PlayerJoin -= Event_PlayerJoin;
            API.Event.PlayerDisconnect -= Event_PlayerDisconnect;
            API.Event.GameWorldSave -= Event_GameWorldSave;
        }

        base.Dispose();
    }

    private void HookEvents()
    {
        API.Event.PlayerJoin += Event_PlayerJoin;
        API.Event.PlayerDisconnect += Event_PlayerDisconnect;
        API.Event.GameWorldSave += Event_GameWorldSave;
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("character")
            .WithAlias("char")
            .WithDescription(T("rpchar-cmd-character-desc"))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .BeginSubCommand("list")
                .WithDescription(T("rpchar-cmd-list-desc"))
                .HandleWith(HandleListCharacters)
            .EndSubCommand()
            .BeginSubCommand("current")
                .WithDescription(T("rpchar-cmd-current-desc"))
                .HandleWith(HandleCurrentCharacter)
            .EndSubCommand()
            .BeginSubCommand("create")
                .WithDescription(T("rpchar-cmd-create-desc"))
                .WithArgs(new StringArgParser(T("rpchar-arg-character-name"), true))
                .HandleWith(HandleCreateCharacter)
            .EndSubCommand()
            .BeginSubCommand("select")
                .WithAlias("switch")
                .WithDescription(T("rpchar-cmd-select-desc"))
                .WithArgs(new StringArgParser(T("rpchar-arg-character-id-or-name"), true))
                .HandleWith(HandleSelectCharacter)
            .EndSubCommand()
            .BeginSubCommand("rename")
                .WithDescription(T("rpchar-cmd-rename-desc"))
                .WithArgs(new WordArgParser(T("rpchar-arg-character-id"), true), new StringArgParser(T("rpchar-arg-new-name"), true))
                .HandleWith(HandleRenameCharacter)
            .EndSubCommand()
            .BeginSubCommand("archive")
                .WithDescription(T("rpchar-cmd-archive-desc"))
                .WithArgs(new StringArgParser(T("rpchar-arg-character-id-or-name"), true))
                .HandleWith(HandleArchiveCharacter)
            .EndSubCommand();

        API.ChatCommands.GetOrCreate("thebasics")
            .BeginSubCommand("character")
                .WithDescription(T("rpchar-cmd-admin-character-desc"))
                .RequiresPrivilege(Privilege.root)
                .BeginSubCommand("list")
                    .WithDescription(T("rpchar-cmd-admin-list-desc"))
                    .WithArgs(new PlayersArgParser(T("rpchar-arg-player"), API, true))
                    .HandleWith(HandleAdminListCharacters)
                .EndSubCommand()
                .BeginSubCommand("select")
                    .WithDescription(T("rpchar-cmd-admin-select-desc"))
                    .WithArgs(new PlayersArgParser(T("rpchar-arg-player"), API, true), new StringArgParser(T("rpchar-arg-character-id-or-name"), true))
                    .HandleWith(HandleAdminSelectCharacter)
                .EndSubCommand()
            .EndSubCommand();
    }

    private void Event_PlayerJoin(IServerPlayer player)
    {
        _characters.EnsureRegistry(player);
    }

    private void Event_PlayerDisconnect(IServerPlayer player)
    {
        _characters.CaptureActiveCharacterState(player);
    }

    private void Event_GameWorldSave()
    {
        foreach (var onlinePlayer in API.World.AllOnlinePlayers)
        {
            if (onlinePlayer is IServerPlayer serverPlayer)
            {
                _characters.CaptureActiveCharacterState(serverPlayer);
            }
        }
    }

    private TextCommandResult HandleListCharacters(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var registry = _characters.EnsureRegistry(player);
        var activeId = _characters.GetActiveCharacterId(player);
        var lines = new StringBuilder(T("rpchar-list-header"));

        foreach (var character in registry.Characters.Where(character => !character.Archived).OrderBy(character => character.DisplayName))
        {
            var activeMarker = character.CharacterId.Equals(activeId, System.StringComparison.OrdinalIgnoreCase) ? "*" : "-";
            lines.Append('\n').Append(T("rpchar-list-item", activeMarker, Safe(character.DisplayName), character.CharacterId));
        }

        return Success(lines.ToString());
    }

    private TextCommandResult HandleCurrentCharacter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        _characters.EnsureRegistry(player);
        var active = _characters.GetActiveCharacter(player);
        return active == null
            ? Error(T("rpchar-error-no-active"))
            : Success(T("rpchar-current", Safe(active.DisplayName), active.CharacterId));
    }

    private TextCommandResult HandleCreateCharacter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var displayName = (string)args.Parsers[0].GetValue();
        return ToCommandResult(_characters.CreateCharacter(player, displayName, Config.MaxRpCharacterSlots));
    }

    private TextCommandResult HandleSelectCharacter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var characterIdOrName = (string)args.Parsers[0].GetValue();
        var preActiveId = _characters.GetActiveCharacterId(player);
        var result = _characters.SelectCharacter(player, characterIdOrName);
        if (result.Success)
        {
            RefreshIdentityState(player);
            if (DidSwitchCharacter(result, preActiveId))
            {
                API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) switched to RP character {result.Character?.DisplayName} ({result.Character?.CharacterId}).");
            }
        }

        return ToCommandResult(result);
    }

    private TextCommandResult HandleRenameCharacter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var characterId = (string)args.Parsers[0].GetValue();
        var displayName = (string)args.Parsers[1].GetValue();
        return ToCommandResult(_characters.RenameCharacter(player, characterId, displayName));
    }

    private TextCommandResult HandleArchiveCharacter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var characterIdOrName = (string)args.Parsers[0].GetValue();
        return ToCommandResult(_characters.ArchiveCharacter(player, characterIdOrName));
    }

    private TextCommandResult HandleAdminListCharacters(TextCommandCallingArgs args)
    {
        var target = GetOnlineTarget(args, 0);
        if (target == null)
        {
            return Error(T("rpchar-error-target-offline"));
        }

        var registry = _characters.EnsureRegistry(target);
        var activeId = _characters.GetActiveCharacterId(target);
        var lines = new StringBuilder(T("rpchar-admin-list-header", Safe(target.PlayerName)));
        foreach (var character in registry.Characters.OrderBy(character => character.Archived).ThenBy(character => character.DisplayName))
        {
            var activeMarker = character.CharacterId.Equals(activeId, System.StringComparison.OrdinalIgnoreCase) ? "*" : "-";
            var archivedMarker = character.Archived ? T("rpchar-list-archived-marker") : string.Empty;
            lines.Append('\n').Append(T("rpchar-list-item", activeMarker, Safe(character.DisplayName), character.CharacterId)).Append(archivedMarker);
        }

        return Success(lines.ToString());
    }

    private TextCommandResult HandleAdminSelectCharacter(TextCommandCallingArgs args)
    {
        var target = GetOnlineTarget(args, 0);
        if (target == null)
        {
            return Error(T("rpchar-error-target-offline"));
        }

        var characterIdOrName = (string)args.Parsers[1].GetValue();
        var preActiveId = _characters.GetActiveCharacterId(target);
        var result = _characters.SelectCharacter(target, characterIdOrName);
        if (result.Success)
        {
            RefreshIdentityState(target);
            if (DidSwitchCharacter(result, preActiveId))
            {
                API.Logger.Audit($"Admin {args.Caller.Player?.PlayerName ?? "server"} forced {target.PlayerName} ({target.PlayerUID}) to RP character {result.Character?.DisplayName} ({result.Character?.CharacterId}).");
            }
        }

        return ToCommandResult(result);
    }

    private IServerPlayer GetOnlineTarget(TextCommandCallingArgs args, int parserIndex)
    {
        var players = args.Parsers[parserIndex].GetValue() as PlayerUidName[];
        if (players == null || players.Length == 0)
        {
            return null;
        }

        return API.GetPlayerByUID(players[0].Uid);
    }

    private static bool DidSwitchCharacter(RpCharacterOperationResult result, string preActiveId)
    {
        return result.Character != null &&
               !result.Character.CharacterId.Equals(preActiveId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshIdentityState(IServerPlayer player)
    {
        API.ModLoader.GetModSystem<ProximityChat.RPProximityChatSystem>()?.RefreshPlayerIdentityState(player);
    }

    private static TextCommandResult ToCommandResult(RpCharacterOperationResult result)
    {
        return result.Success ? Success(result.Message) : Error(result.Message);
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = message
        };
    }

    private static TextCommandResult Error(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Error,
            StatusMessage = message
        };
    }

    private static string T(string key, params object[] args)
    {
        return Lang.Get("thebasics:" + key, args);
    }

    private static string Safe(string value)
    {
        return VtmlUtils.EscapeVtml(value ?? string.Empty);
    }
}
