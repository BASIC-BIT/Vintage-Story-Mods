using System;
using System.Globalization;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.CharacterSheets;

public class CharacterSheetSystem : BaseBasicModSystem
{
    private const string ModDataKey = "BASIC_CHARACTER_SHEET";
    private const string NicknameBind = "thebasics.nickname";

    protected override void BasicStartServerSide()
    {
        if (!Config.EnableCharacterSheets)
        {
            return;
        }

        RegisterCommands();
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("charsheet")
            .WithAlias("sheet", "bio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-view-desc"))
            .RequiresPrivilege(Privilege.chat)
            .WithArgs(new PlayersArgParser("player", API, false))
            .HandleWith(ViewSheet);

        API.ChatCommands.GetOrCreate("charsheetfields")
            .WithAlias("sheetfields", "biofields")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-fields-desc"))
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(ListFields);

        API.ChatCommands.GetOrCreate("setcharsheet")
            .WithAlias("setsheet", "setbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-set-desc"))
            .RequiresPrivilege(Config.CharacterSheetSetPermission)
            .RequiresPlayer()
            .WithArgs(new WordArgParser("field", true), new StringArgParser("value", true))
            .HandleWith(SetOwnField);

        API.ChatCommands.GetOrCreate("clearcharsheet")
            .WithAlias("clearsheet", "clearbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-clear-desc"))
            .RequiresPrivilege(Config.CharacterSheetSetPermission)
            .RequiresPlayer()
            .WithArgs(new WordArgParser("field", false))
            .HandleWith(ClearOwnField);

        API.ChatCommands.GetOrCreate("adminsetcharsheet")
            .WithAlias("adminsetsheet", "adminsetbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-admin-set-desc"))
            .RequiresPrivilege(Config.CharacterSheetAdminPermission)
            .WithArgs(new PlayersArgParser("player", API, true), new WordArgParser("field", true), new StringArgParser("value", true))
            .HandleWith(SetAdminField);

        API.ChatCommands.GetOrCreate("adminclearcharsheet")
            .WithAlias("adminclearsheet", "adminclearbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-admin-clear-desc"))
            .RequiresPrivilege(Config.CharacterSheetAdminPermission)
            .WithArgs(new PlayersArgParser("player", API, true), new WordArgParser("field", false))
            .HandleWith(ClearAdminField);
    }

    private TextCommandResult ViewSheet(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: true);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var isOwnSheet = args.Caller.Type == EnumCallerType.Player && args.Caller.Player.PlayerUID == target.PlayerUID;
        var data = GetSheetData(target);
        var message = new StringBuilder(isOwnSheet
            ? Lang.Get("thebasics:charsheet-header-own")
            : Lang.Get("thebasics:charsheet-header-other", VtmlUtils.EscapeVtml(target.PlayerName)));

        var renderedFields = 0;
        foreach (var field in Config.CharacterSheetFields)
        {
            var value = GetFieldValue(target, data, field);
            if (string.IsNullOrWhiteSpace(value) && field.Optional)
            {
                continue;
            }

            renderedFields++;
            message.Append('\n');
            message.Append(VtmlUtils.EscapeVtml(GetFieldLabel(field)));
            message.Append(": ");
            message.Append(string.IsNullOrWhiteSpace(value) ? Lang.Get("thebasics:charsheet-unset") : VtmlUtils.EscapeVtml(value));
        }

        if (renderedFields == 0)
        {
            message.Append('\n');
            message.Append(Lang.Get("thebasics:charsheet-empty"));
        }

        return Success(message.ToString());
    }

    private TextCommandResult ListFields(TextCommandCallingArgs args)
    {
        var message = new StringBuilder(Lang.Get("thebasics:charsheet-fields-header"));
        foreach (var field in Config.CharacterSheetFields)
        {
            message.Append('\n');
            message.Append(VtmlUtils.EscapeVtml(GetFieldId(field)));
            message.Append(" - ");
            message.Append(VtmlUtils.EscapeVtml(GetFieldLabel(field)));
            message.Append(" (");
            message.Append(VtmlUtils.EscapeVtml(GetFieldType(field)));
            message.Append(field.Optional ? ", optional" : ", required");
            if (IsNicknameField(field))
            {
                message.Append(", nickname");
            }
            if (IsOptionField(field))
            {
                message.Append(", options: ");
                message.Append(VtmlUtils.EscapeVtml(string.Join(", ", field.Options)));
            }
            message.Append(')');
        }

        return Success(message.ToString());
    }

    private TextCommandResult SetOwnField(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        return SetField(player, args.Parsers[0].GetValue()?.ToString(), args.Parsers[1].GetValue()?.ToString(), isAdminAction: false);
    }

    private TextCommandResult ClearOwnField(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        return ClearField(player, args.Parsers[0].IsMissing ? null : args.Parsers[0].GetValue()?.ToString());
    }

    private TextCommandResult SetAdminField(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        return SetField(target, args.Parsers[1].GetValue()?.ToString(), args.Parsers[2].GetValue()?.ToString(), isAdminAction: true);
    }

    private TextCommandResult ClearAdminField(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        return ClearField(target, args.Parsers[1].IsMissing ? null : args.Parsers[1].GetValue()?.ToString());
    }

    private TextCommandResult SetField(IServerPlayer player, string fieldName, string value, bool isAdminAction)
    {
        var field = ResolveField(fieldName);
        if (field == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-not-found", fieldName, GetValidFieldList()));
        }

        value = (value ?? string.Empty).Trim();
        if (!TryNormalizeValue(field, value, out var normalizedValue, out var errorMessage))
        {
            return Error(errorMessage);
        }

        if (IsNicknameField(field))
        {
            if (!NicknameValidationUtils.ValidateNickname(player, normalizedValue, API, out var conflictingPlayer, out var conflictType))
            {
                return Error(Lang.Get("thebasics:chat-nick-conflict", normalizedValue, conflictingPlayer, conflictType));
            }

            player.SetNickname(normalizedValue);
            RefreshNameTag(player);
        }
        else
        {
            var data = GetSheetData(player);
            data.Fields[GetFieldId(field)] = normalizedValue;
            SaveSheetData(player, data);
        }

        API.Logger.Audit($"{(isAdminAction ? "Admin" : "Player")} updated character sheet field '{GetFieldId(field)}' for {player.PlayerName}.");
        return Success(Lang.Get("thebasics:charsheet-success-set", VtmlUtils.EscapeVtml(GetFieldLabel(field)), VtmlUtils.EscapeVtml(player.PlayerName)));
    }

    private TextCommandResult ClearField(IServerPlayer player, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            var data = GetSheetData(player);
            data.Fields.Clear();
            SaveSheetData(player, data);

            foreach (var field in Config.CharacterSheetFields.Where(IsNicknameField))
            {
                player.ClearNickname();
            }
            RefreshNameTag(player);

            API.Logger.Audit($"Character sheet cleared for {player.PlayerName}.");
            return Success(Lang.Get("thebasics:charsheet-success-cleared-all", VtmlUtils.EscapeVtml(player.PlayerName)));
        }

        var resolvedField = ResolveField(fieldName);
        if (resolvedField == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-not-found", fieldName, GetValidFieldList()));
        }

        if (IsNicknameField(resolvedField))
        {
            player.ClearNickname();
            RefreshNameTag(player);
        }
        else
        {
            var data = GetSheetData(player);
            data.Fields.Remove(GetFieldId(resolvedField));
            SaveSheetData(player, data);
        }

        API.Logger.Audit($"Character sheet field '{GetFieldId(resolvedField)}' cleared for {player.PlayerName}.");
        return Success(Lang.Get("thebasics:charsheet-success-cleared-field", VtmlUtils.EscapeVtml(GetFieldLabel(resolvedField)), VtmlUtils.EscapeVtml(player.PlayerName)));
    }

    private bool TryNormalizeValue(CharacterSheetFieldDefinition field, string value, out string normalizedValue, out string errorMessage)
    {
        normalizedValue = value;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(value) && !field.Optional)
        {
            errorMessage = Lang.Get("thebasics:charsheet-error-required", GetFieldLabel(field));
            return false;
        }

        var maxLength = GetMaxLength(field);
        if (maxLength > 0 && value.Length > maxLength)
        {
            errorMessage = Lang.Get("thebasics:charsheet-error-too-long", GetFieldLabel(field), maxLength);
            return false;
        }

        if (IsNumberField(field))
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                errorMessage = Lang.Get("thebasics:charsheet-error-number", GetFieldLabel(field));
                return false;
            }

            normalizedValue = number.ToString(CultureInfo.InvariantCulture);
        }

        if (IsOptionField(field))
        {
            var option = field.Options.FirstOrDefault(option => option.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (option == null)
            {
                errorMessage = Lang.Get("thebasics:charsheet-error-option", GetFieldLabel(field), string.Join(", ", field.Options));
                return false;
            }

            normalizedValue = option;
        }

        return true;
    }

    private IServerPlayer ResolveTarget(TextCommandCallingArgs args, int parserIndex, bool allowSelfFallback)
    {
        if (args.Parsers[parserIndex].IsMissing)
        {
            return allowSelfFallback && args.Caller.Type == EnumCallerType.Player
                ? (IServerPlayer)args.Caller.Player
                : null;
        }

        var playerArg = ((PlayerUidName[])args.Parsers[parserIndex].GetValue()).FirstOrDefault();
        return playerArg == null ? null : API.GetPlayerByUID(playerArg.Uid);
    }

    private CharacterSheetFieldDefinition ResolveField(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        return Config.CharacterSheetFields.FirstOrDefault(field =>
            GetFieldId(field).Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
            GetFieldLabel(field).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private CharacterSheetData GetSheetData(IServerPlayer player)
    {
        var data = player.GetModData<CharacterSheetData>(ModDataKey, new CharacterSheetData()) ?? new CharacterSheetData();
        data.Fields ??= new System.Collections.Generic.Dictionary<string, string>();
        return data;
    }

    private void SaveSheetData(IServerPlayer player, CharacterSheetData data)
    {
        player.SetModData(ModDataKey, data);
    }

    private string GetFieldValue(IServerPlayer player, CharacterSheetData data, CharacterSheetFieldDefinition field)
    {
        if (IsNicknameField(field))
        {
            return player.HasNickname() ? player.GetNickname() : string.Empty;
        }

        return data.Fields.TryGetValue(GetFieldId(field), out var value) ? value : string.Empty;
    }

    private string GetValidFieldList()
    {
        return string.Join(", ", Config.CharacterSheetFields.Select(GetFieldId));
    }

    private int GetMaxLength(CharacterSheetFieldDefinition field)
    {
        if (field.MaxLength > 0)
        {
            return field.MaxLength;
        }

        return IsNicknameField(field) ? Config.MaxNicknameLength : 0;
    }

    private static string GetFieldId(CharacterSheetFieldDefinition field)
    {
        return (field.Id ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string GetFieldLabel(CharacterSheetFieldDefinition field)
    {
        return string.IsNullOrWhiteSpace(field.Label) ? GetFieldId(field) : field.Label.Trim();
    }

    private static string GetFieldType(CharacterSheetFieldDefinition field)
    {
        return string.IsNullOrWhiteSpace(field.Type) ? CharacterSheetFieldTypes.String : field.Type.Trim().ToLowerInvariant();
    }

    private static bool IsNicknameField(CharacterSheetFieldDefinition field)
    {
        return field.BindTo?.Equals(NicknameBind, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsNumberField(CharacterSheetFieldDefinition field)
    {
        return GetFieldType(field) == CharacterSheetFieldTypes.Number;
    }

    private static bool IsOptionField(CharacterSheetFieldDefinition field)
    {
        return GetFieldType(field) == CharacterSheetFieldTypes.Option;
    }

    private void RefreshNameTag(IServerPlayer player)
    {
        var behavior = player.Entity?.GetBehavior<EntityBehaviorNameTag>();
        if (behavior == null)
        {
            return;
        }

        behavior.ShowOnlyWhenTargeted = Config.HideNametagUnlessTargeting;
        behavior.RenderRange = Config.NametagRenderRange;

        string displayName;
        if (Config.ShowNicknameInNametag)
        {
            var nickname = player.GetNickname();
            displayName = string.IsNullOrWhiteSpace(nickname)
                ? (Config.ShowPlayerNameInNametag ? player.PlayerName : string.Empty)
                : (Config.ShowPlayerNameInNametag ? $"{nickname} ({player.PlayerName})" : nickname);
        }
        else
        {
            displayName = Config.ShowPlayerNameInNametag ? player.PlayerName : string.Empty;
        }

        behavior.SetName(displayName);
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = message,
        };
    }

    private static TextCommandResult Error(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Error,
            StatusMessage = message,
        };
    }
}
