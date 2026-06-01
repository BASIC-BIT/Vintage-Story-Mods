#pragma warning disable S1541, S3776 // Character sheet command/network orchestration needs a dedicated behavior-preserving refactor.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using thebasics.Utilities.Parsers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.CharacterSheets;

public class CharacterSheetSystem : BaseBasicModSystem
{
    private const string ModDataKey = "BASIC_CHARACTER_SHEET";
    private const string NicknameBind = "thebasics.nickname";
    private const string FullNameBind = "thebasics.fullName";
    /// <summary>
    /// Sub-key inside the entity's "nametag" tree-attribute. Stored there (rather than at
    /// top-level WatchedAttributes) so vanilla's <c>EntityBehaviorNameTag.OnNameChanged</c>
    /// listener — which fires on any "nametag" path change — automatically rebuilds the
    /// texture when the headshot updates.
    /// </summary>
    internal const string NametagAttrTree = "nametag";
    internal const string HeadshotHashAttrKey = "thebasicsHeadshotHash";

    /// <summary>
    /// Sub-key for the player's nickname color (hex string with leading '#'), or empty when none.
    /// Lives next to <see cref="HeadshotHashAttrKey"/> for the same OnNameChanged-triggering reason.
    /// </summary>
    internal const string NicknameColorAttrKey = "thebasicsNicknameColor";

    private HeadshotStore _headshotStore;
    private readonly Dictionary<string, long> _lastHeadshotUploadAtMs = new();

    protected override void BasicStartServerSide()
    {
        if (!Config.EnableCharacterSheets)
        {
            return;
        }

        if (Config.EnableCharacterHeadshots)
        {
            _headshotStore = new HeadshotStore(API);
            API.Event.PlayerDisconnect += OnPlayerDisconnect;
            API.Event.PlayerJoin += OnPlayerJoin;
        }

        RegisterCommands();
    }

    /// <summary>
    /// Backfills the player's <see cref="HeadshotHashAttrKey"/> watched attribute from their saved
    /// <see cref="CharacterSheetData.Headshot"/>. Needed because pre-existing players from before
    /// nametag-headshot rendering shipped won't have the watched attribute set yet.
    /// </summary>
    private static void OnPlayerJoin(IServerPlayer player)
    {
        if (player?.Entity == null) return;
        var data = GetSheetData(player);
        SyncHeadshotHashAttr(player, data?.Headshot?.Hash);
        SyncNicknameColorAttr(player);
    }

    private static void SyncHeadshotHashAttr(IServerPlayer player, string hash)
    {
        if (player?.Entity?.WatchedAttributes is not { } attrs) return;
        var nametag = attrs.GetTreeAttribute(NametagAttrTree);
        if (nametag == null) return; // The vanilla EntityBehaviorNameTag creates this on entity init.
        var safe = hash ?? string.Empty;
        if (nametag.GetString(HeadshotHashAttrKey) == safe) return;
        nametag.SetString(HeadshotHashAttrKey, safe);
        attrs.MarkPathDirty(NametagAttrTree);
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("charsheet")
            .WithAlias("sheet")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-view-desc"))
            .RequiresPrivilege(Privilege.chat)
            .WithArgs(new PlayerByNameOrNicknameArgParser("player", API, false, Config))
            .HandleWith(ViewSheet);

        API.ChatCommands.GetOrCreate("look")
            .WithAlias("inspect")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-look-desc"))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .WithArgs(new PlayerByNameOrNicknameArgParser("player", API, false, Config))
            .HandleWith(LookAtPlayer);

        API.ChatCommands.GetOrCreate("bio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-view-desc"))
            .RequiresPrivilege(Privilege.chat)
            .WithArgs(new PlayerByNameOrNicknameArgParser("player", API, false, Config))
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

        API.ChatCommands.GetOrCreate("adminviewcharsheet")
            .WithAlias("adminviewsheet", "adminviewbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-admin-view-desc"))
            .RequiresPrivilege(Config.CharacterSheetAdminPermission)
            .WithArgs(new PlayersArgParser("player", API, true))
            .HandleWith(ViewAdminSheet);

        API.ChatCommands.GetOrCreate("adminclearcharsheet")
            .WithAlias("adminclearsheet", "adminclearbio")
            .WithDescription(Lang.Get("thebasics:charsheet-cmd-admin-clear-desc"))
            .RequiresPrivilege(Config.CharacterSheetAdminPermission)
            .WithArgs(new PlayersArgParser("player", API, true), new WordArgParser("field", false))
            .HandleWith(ClearAdminField);

        if (Config.EnableCharacterHeadshots)
        {
            API.ChatCommands.GetOrCreate("clearheadshot")
                .WithDescription(Lang.Get("thebasics:headshot-cmd-clear-desc"))
                .RequiresPrivilege(Config.CharacterSheetSetPermission)
                .RequiresPlayer()
                .HandleWith(ClearOwnHeadshot);

            API.ChatCommands.GetOrCreate("adminclearheadshot")
                .WithDescription(Lang.Get("thebasics:headshot-cmd-admin-clear-desc"))
                .RequiresPrivilege(Config.CharacterSheetAdminPermission)
                .WithArgs(new PlayersArgParser("player", API, true))
                .HandleWith(ClearAdminHeadshot);
        }
    }

    private TextCommandResult ClearOwnHeadshot(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var ok = ClearHeadshot(player);
        if (!ok)
        {
            return Error(Lang.Get("thebasics:headshot-error-clear-failed"));
        }

        API.Logger.Audit($"Player {player.PlayerName} cleared their headshot.");
        return Success(Lang.Get("thebasics:headshot-clear-success-self"));
    }

    private TextCommandResult ClearAdminHeadshot(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var ok = ClearHeadshot(target);
        if (!ok)
        {
            return Error(Lang.Get("thebasics:headshot-error-clear-failed"));
        }

        var editorName = args.Caller.Type == EnumCallerType.Player ? args.Caller.Player?.PlayerName ?? "console" : "console";
        API.Logger.Audit($"Admin {editorName} cleared headshot for {target.PlayerName}.");
        return Success(Lang.Get("thebasics:headshot-clear-success-other", VtmlUtils.EscapeVtml(target.PlayerName)));
    }

    internal CharacterSheetViewMessage BuildClientView(IServerPlayer viewer, CharacterSheetOpenRequest request)
    {
        if (!Config.EnableCharacterSheets)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-gui-disabled"), CharacterSheetViewMessage.ErrorCodeDisabled);
        }

        var mode = (request?.Mode ?? CharacterSheetOpenRequest.ModeOwn).Trim().ToLowerInvariant();
        return mode switch
        {
            CharacterSheetOpenRequest.ModeLook => BuildLookClientView(viewer, request),
            CharacterSheetOpenRequest.ModeAdmin => BuildAdminClientView(viewer, request),
            CharacterSheetOpenRequest.ModeView => BuildRegularClientView(viewer, request),
            _ => BuildSheetViewMessage(viewer, viewer, CharacterSheetViewMode.Full, includeEmpty: true, canEdit: true, isAdminView: false)
        };
    }

    internal CharacterSheetViewMessage SaveClientFields(IServerPlayer editor, CharacterSheetSaveRequest request)
    {
        if (!Config.EnableCharacterSheets)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-gui-disabled"), CharacterSheetViewMessage.ErrorCodeDisabled);
        }

        var isAdminAction = request?.IsAdminAction == true;
        if (isAdminAction && !editor.HasPrivilege(Config.CharacterSheetAdminPermission))
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-admin-privilege"));
        }

        var target = isAdminAction ? API.GetPlayerByUID(request.TargetPlayerUid) : editor;
        if (target == null)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        if (!TryBuildClientFieldChanges(editor, target, request, isAdminAction, out var changes, out var errorMessage))
        {
            return CreateErrorView(errorMessage);
        }

        if (!ApplyClientFieldChanges(target, changes, out errorMessage))
        {
            return CreateErrorView(errorMessage);
        }

        API.Logger.Audit($"{(isAdminAction ? "Admin" : "Player")} saved character sheet for {target.PlayerName}.");
        var response = BuildSheetViewMessage(editor, target, isAdminAction ? CharacterSheetViewMode.Admin : CharacterSheetViewMode.Full, includeEmpty: true, canEdit: true, isAdminView: isAdminAction);
        response.Message = Lang.Get("thebasics:charsheet-gui-saved", VtmlUtils.EscapeVtml(response.DisplayName));
        response.IsSaveResponse = true;
        return response;
    }

    internal HeadshotUploadResult HandleHeadshotUpload(IServerPlayer sender, HeadshotUploadRequest request)
    {
        if (!Config.EnableCharacterSheets || !Config.EnableCharacterHeadshots || _headshotStore == null)
        {
            return HeadshotUploadFail(sender?.PlayerUID, Lang.Get("thebasics:headshot-error-disabled"));
        }

        if (sender == null || request == null)
        {
            return HeadshotUploadFail(sender?.PlayerUID, Lang.Get("thebasics:headshot-error-bad-request"));
        }

        var isAdminAction = !string.IsNullOrWhiteSpace(request.AdminTargetPlayerUid)
                            && !request.AdminTargetPlayerUid.Equals(sender.PlayerUID, StringComparison.Ordinal);

        if (isAdminAction && !sender.HasPrivilege(Config.CharacterSheetAdminPermission))
        {
            return HeadshotUploadFail(sender.PlayerUID, Lang.Get("thebasics:charsheet-error-admin-privilege"));
        }

        var target = isAdminAction
            ? API.GetPlayerByUID(request.AdminTargetPlayerUid)
            : sender;
        if (target == null)
        {
            return HeadshotUploadFail(sender.PlayerUID, Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        if (!isAdminAction && !sender.HasPrivilege(Config.CharacterSheetSetPermission))
        {
            return HeadshotUploadFail(sender.PlayerUID, Lang.Get("thebasics:headshot-error-no-permission"));
        }

        var maxKb = Math.Max(1, Config.HeadshotMaxKb);
        var maxOutputBytes = maxKb * 1024;
        var inputBytes = request.PngBytes;
        if (inputBytes == null || inputBytes.Length == 0)
        {
            return HeadshotUploadFail(target.PlayerUID, Lang.Get("thebasics:headshot-error-empty"));
        }

        // Pre-decode size guard: even before decoding, the *encoded* input shouldn't exceed our cap by much.
        // Allow up to 4x the post-normalization budget for the encoded input (heuristic for JPEGs that compress well).
        var inputMaxBytes = Math.Max(maxOutputBytes * 4, 64 * 1024);
        if (inputBytes.Length > inputMaxBytes)
        {
            return HeadshotUploadFail(target.PlayerUID, Lang.Get("thebasics:headshot-error-too-large", maxKb));
        }

        if (!isAdminAction)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastHeadshotUploadAtMs.TryGetValue(sender.PlayerUID, out var lastMs))
            {
                var cooldownMs = Math.Max(0, Config.HeadshotUploadCooldownSec) * 1000L;
                var sinceMs = nowMs - lastMs;
                if (sinceMs < cooldownMs)
                {
                    var waitSec = (int)Math.Ceiling((cooldownMs - sinceMs) / 1000.0);
                    return HeadshotUploadFail(target.PlayerUID, Lang.Get("thebasics:headshot-error-cooldown", waitSec));
                }
            }
        }

        var options = new HeadshotPipeline.NormalizeOptions(
            TargetDimension: Math.Max(16, Config.HeadshotMaxDimension),
            MaxOutputBytes: maxOutputBytes,
            MaxDecodedDimensionEitherAxis: Math.Max(64, Config.HeadshotMaxDecodedDimension));
        var result = HeadshotPipeline.Normalize(inputBytes, options);
        if (!result.Ok)
        {
            TrackHeadshotUploadFailure(result.ErrorCode);
            return HeadshotUploadFail(target.PlayerUID, HeadshotPipeline.GetErrorMessage(result.ErrorCode, maxKb));
        }

        var characterId = target.GetActiveRpCharacterId();
        if (!_headshotStore.TryWrite(target.PlayerUID, characterId, result.PngBytes))
        {
            TrackHeadshotUploadFailure("write_failed");
            return HeadshotUploadFail(target.PlayerUID, Lang.Get("thebasics:headshot-error-write"));
        }

        var data = GetSheetData(target);
        data.Headshot = HeadshotPipeline.BuildMetadata(result, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        SaveSheetData(target, data);
        SyncHeadshotHashAttr(target, result.Hash);

        if (!isAdminAction)
        {
            _lastHeadshotUploadAtMs[sender.PlayerUID] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // Hash from ComputeSha256Hex is always 64 hex chars.
        API.Logger.Audit($"{(isAdminAction ? "Admin" : "Player")} {sender.PlayerName} uploaded headshot for {target.PlayerName} ({result.PngBytes.Length} bytes, hash {result.Hash[..8]}).");

        AnalyticsService.TrackFeatureUsed("character_headshot", "upload");

        return new HeadshotUploadResult
        {
            Success = true,
            TargetPlayerUid = target.PlayerUID,
            Metadata = data.Headshot,
            Message = Lang.Get("thebasics:headshot-upload-success")
        };
    }

    private static void TrackHeadshotUploadFailure(string resultCode)
    {
        var normalizedResultCode = string.IsNullOrWhiteSpace(resultCode) ? "failure" : resultCode;
        AnalyticsService.TrackFeatureUsed("character_headshot", "upload", false, normalizedResultCode);
        if (normalizedResultCode == HeadshotErrorCodes.Exception || normalizedResultCode == "write_failed")
        {
            AnalyticsService.TrackFailure("character_headshot", "upload", "error", normalizedResultCode);
        }
    }

    internal HeadshotFetchResult HandleHeadshotFetch(IServerPlayer sender, HeadshotFetchRequest request)
    {
        if (!Config.EnableCharacterSheets || !Config.EnableCharacterHeadshots || _headshotStore == null)
        {
            return new HeadshotFetchResult { TargetPlayerUid = request?.TargetPlayerUid ?? string.Empty };
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TargetPlayerUid))
        {
            return new HeadshotFetchResult();
        }

        var target = API.GetPlayerByUID(request.TargetPlayerUid);
        if (target == null)
        {
            return new HeadshotFetchResult { TargetPlayerUid = request.TargetPlayerUid };
        }

        var data = GetSheetData(target);
        var metadata = data.Headshot;
        if (metadata == null || string.IsNullOrEmpty(metadata.Hash))
        {
            return new HeadshotFetchResult { TargetPlayerUid = request.TargetPlayerUid };
        }

        var result = new HeadshotFetchResult
        {
            TargetPlayerUid = request.TargetPlayerUid,
            Hash = metadata.Hash,
            Width = metadata.Width,
            Height = metadata.Height
        };

        if (!string.IsNullOrEmpty(request.ClientCachedHash) && string.Equals(request.ClientCachedHash, metadata.Hash, StringComparison.Ordinal))
        {
            return result;
        }

        var characterId = target.GetActiveRpCharacterId();
        if (_headshotStore.TryRead(target.PlayerUID, characterId, out var bytes))
        {
            result.PngBytes = bytes;
            return result;
        }

        // File missing at the resolved path. Don't auto-wipe metadata — character-id or
        // file-system timing races have caused false positives in the past, and silently
        // erasing a player's portrait on a single missed read is unrecoverable. Fall back to
        // scanning the player's headshot directory for any file matching `<uid>_*.png`
        // (covers RP character-id drift after rejoin); if we find one, serve those bytes.
        if (_headshotStore.TryFindAnyForPlayer(target.PlayerUID, out var fallbackBytes))
        {
            result.PngBytes = fallbackBytes;
            return result;
        }

        // Truly nothing on disk for this player; surface the empty state but keep the metadata
        // pointer alone so a re-upload (or admin /adminclearheadshot) is the only way to clear it.
        result.Hash = string.Empty;
        result.Width = 0;
        result.Height = 0;
        return result;
    }

    internal bool ClearHeadshot(IServerPlayer target)
    {
        if (!Config.EnableCharacterSheets || !Config.EnableCharacterHeadshots || _headshotStore == null || target == null)
        {
            return false;
        }

        var characterId = target.GetActiveRpCharacterId();
        var deleted = _headshotStore.TryDelete(target.PlayerUID, characterId);
        var data = GetSheetData(target);
        if (data.Headshot != null)
        {
            data.Headshot = null;
            SaveSheetData(target, data);
        }
        SyncHeadshotHashAttr(target, null);
        return deleted;
    }

    internal HeadshotUploadResult HandleHeadshotClear(IServerPlayer sender, HeadshotClearRequest request)
    {
        if (!Config.EnableCharacterSheets || !Config.EnableCharacterHeadshots || _headshotStore == null)
        {
            return HeadshotUploadFail(sender?.PlayerUID, Lang.Get("thebasics:headshot-error-disabled"));
        }

        if (sender == null)
        {
            return HeadshotUploadFail(null, Lang.Get("thebasics:headshot-error-bad-request"));
        }

        var isAdminAction = !string.IsNullOrWhiteSpace(request?.AdminTargetPlayerUid)
                            && !request.AdminTargetPlayerUid.Equals(sender.PlayerUID, StringComparison.Ordinal);

        if (isAdminAction && !sender.HasPrivilege(Config.CharacterSheetAdminPermission))
        {
            return HeadshotUploadFail(sender.PlayerUID, Lang.Get("thebasics:charsheet-error-admin-privilege"));
        }

        var target = isAdminAction ? API.GetPlayerByUID(request.AdminTargetPlayerUid) : sender;
        if (target == null)
        {
            return HeadshotUploadFail(sender.PlayerUID, Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var ok = ClearHeadshot(target);
        if (!ok)
        {
            return HeadshotUploadFail(target.PlayerUID, Lang.Get("thebasics:headshot-error-clear-failed"));
        }

        API.Logger.Audit($"{(isAdminAction ? "Admin" : "Player")} {sender.PlayerName} cleared headshot for {target.PlayerName}.");
        return new HeadshotUploadResult
        {
            Success = true,
            TargetPlayerUid = target.PlayerUID,
            Metadata = null,
            Message = isAdminAction
                ? Lang.Get("thebasics:headshot-clear-success-other", target.PlayerName)
                : Lang.Get("thebasics:headshot-clear-success-self")
        };
    }

    /// <summary>
    /// Drops the cooldown entry for a leaving player so the dictionary doesn't accumulate
    /// over the lifetime of a long-running server.
    /// </summary>
    internal void OnPlayerDisconnect(IServerPlayer player)
    {
        if (player?.PlayerUID is { } uid)
        {
            _lastHeadshotUploadAtMs.Remove(uid);
        }
    }

    private static HeadshotUploadResult HeadshotUploadFail(string targetUid, string message)
    {
        return new HeadshotUploadResult
        {
            Success = false,
            TargetPlayerUid = targetUid ?? string.Empty,
            Message = message ?? string.Empty
        };
    }


    private bool TryBuildClientFieldChanges(IServerPlayer editor, IServerPlayer target, CharacterSheetSaveRequest request, bool isAdminAction, out List<CharacterSheetFieldChange> changes, out string errorMessage)
    {
        changes = new List<CharacterSheetFieldChange>();
        errorMessage = string.Empty;

        foreach (var submittedField in request?.Fields ?? Array.Empty<CharacterSheetFieldValueMessage>())
        {
            if (!TryCreateClientFieldChange(editor, target, submittedField, isAdminAction, out var change, out errorMessage))
            {
                return false;
            }

            changes.Add(change);
        }

        return true;
    }

    private bool TryCreateClientFieldChange(IServerPlayer editor, IServerPlayer target, CharacterSheetFieldValueMessage submittedField, bool isAdminAction, out CharacterSheetFieldChange change, out string errorMessage)
    {
        change = null;
        var field = ResolveField(submittedField.FieldId);
        if (field == null)
        {
            errorMessage = Lang.Get("thebasics:charsheet-error-field-not-found", submittedField.FieldId, GetValidFieldList());
            return false;
        }

        if (!CanEditField(editor, target, field, isAdminAction))
        {
            errorMessage = Lang.Get("thebasics:charsheet-error-field-readonly", GetFieldLabel(field));
            return false;
        }

        var value = (submittedField.Value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value) && field.Optional)
        {
            change = new CharacterSheetFieldChange(field, string.Empty, Clear: true);
            errorMessage = string.Empty;
            return true;
        }

        if (!TryNormalizeValue(field, value, out var normalizedValue, out errorMessage))
        {
            return false;
        }

        if (IsNicknameField(field) && !NicknameValidationUtils.ValidateNickname(target, normalizedValue, API, Config, out var conflictingPlayer, out var conflictType))
        {
            errorMessage = Lang.Get("thebasics:chat-nick-conflict", normalizedValue, conflictingPlayer, conflictType);
            return false;
        }

        change = new CharacterSheetFieldChange(field, normalizedValue, Clear: false);
        return true;
    }

    private bool ApplyClientFieldChanges(IServerPlayer target, IEnumerable<CharacterSheetFieldChange> changes, out string errorMessage)
    {
        errorMessage = string.Empty;
        foreach (var change in changes)
        {
            if (change.Clear)
            {
                ClearFieldValue(target, change.Field);
                continue;
            }

            if (!TrySetFieldValue(target, change.Field, change.Value, out errorMessage))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record CharacterSheetFieldChange(CharacterSheetFieldDefinition Field, string Value, bool Clear);

    private TextCommandResult ViewSheet(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: true);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var viewer = args.Caller.Type == EnumCallerType.Player ? args.Caller.Player as IServerPlayer : null;

        // When a player runs the command in-game, push the GUI dialog instead of a chat-text view.
        // Console / non-player callers (server scripts, etc.) still get the rendered text response.
        if (viewer != null)
        {
            var isOwnSheet = viewer.PlayerUID == target.PlayerUID;
            var view = BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Full, includeEmpty: isOwnSheet, canEdit: isOwnSheet, isAdminView: false);
            API.ModLoader.GetModSystem<ProximityChat.RPProximityChatSystem>()?.PushCharacterSheetView(viewer, view);
            return Success(string.Empty);
        }

        return Success(RenderSheet(null, target, CharacterSheetViewMode.Full, includeEmpty: false));
    }

    private TextCommandResult ViewAdminSheet(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var viewer = args.Caller.Type == EnumCallerType.Player ? args.Caller.Player as IServerPlayer : null;
        if (viewer != null)
        {
            var view = BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Admin, includeEmpty: true, canEdit: true, isAdminView: true);
            API.ModLoader.GetModSystem<ProximityChat.RPProximityChatSystem>()?.PushCharacterSheetView(viewer, view);
            return Success(string.Empty);
        }

        return Success(RenderSheet(null, target, CharacterSheetViewMode.Admin, includeEmpty: true));
    }

    private TextCommandResult LookAtPlayer(TextCommandCallingArgs args)
    {
        var viewer = (IServerPlayer)args.Caller.Player;
        var target = ResolveLookTarget(args, viewer);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-look-target"));
        }

        if (target.PlayerUID != viewer.PlayerUID)
        {
            var distance = viewer.Entity.Pos.DistanceTo(target.Entity.Pos);
            if (distance > Config.CharacterSheetLookRange)
            {
                return Error(Lang.Get("thebasics:charsheet-error-look-range", Config.CharacterSheetLookRange));
            }

            if (Config.CharacterSheetLookRequiresLineOfSight && !VisibilityUtils.HasLineOfSight(API.World, viewer.Entity, target.Entity))
            {
                return Error(Lang.Get("thebasics:charsheet-error-look-los"));
            }
        }

        // Players get the GUI dialog; LookMode for cross-player so visibility rules + read-only apply.
        var isSelf = target.PlayerUID == viewer.PlayerUID;
        var lookView = BuildSheetViewMessage(viewer, target,
            isSelf ? CharacterSheetViewMode.Full : CharacterSheetViewMode.Look,
            includeEmpty: isSelf,
            canEdit: isSelf,
            isAdminView: false);
        API.ModLoader.GetModSystem<ProximityChat.RPProximityChatSystem>()?.PushCharacterSheetView(viewer, lookView);
        return Success(string.Empty);
    }

    private CharacterSheetViewMessage BuildRegularClientView(IServerPlayer viewer, CharacterSheetOpenRequest request)
    {
        var target = ResolveNetworkTarget(request);
        if (target == null)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-view-target"));
        }

        var isOwnSheet = target.PlayerUID == viewer.PlayerUID;
        return BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Full, includeEmpty: isOwnSheet, canEdit: isOwnSheet, isAdminView: false);
    }

    private CharacterSheetViewMessage BuildAdminClientView(IServerPlayer viewer, CharacterSheetOpenRequest request)
    {
        if (!viewer.HasPrivilege(Config.CharacterSheetAdminPermission))
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-admin-privilege"));
        }

        var target = ResolveNetworkTarget(request);
        return target == null
            ? CreateErrorView(Lang.Get("thebasics:charsheet-error-player-not-found"))
            : BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Admin, includeEmpty: true, canEdit: true, isAdminView: true);
    }

    private CharacterSheetViewMessage BuildLookClientView(IServerPlayer viewer, CharacterSheetOpenRequest request)
    {
        var target = ResolveNetworkTarget(request) ?? ResolveSelectedPlayer(viewer);
        if (target == null)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-look-target"));
        }

        if (target.PlayerUID == viewer.PlayerUID)
        {
            return BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Full, includeEmpty: true, canEdit: true, isAdminView: false);
        }

        var distance = viewer.Entity.Pos.DistanceTo(target.Entity.Pos);
        if (distance > Config.CharacterSheetLookRange)
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-look-range", Config.CharacterSheetLookRange));
        }

        if (Config.CharacterSheetLookRequiresLineOfSight && !VisibilityUtils.HasLineOfSight(API.World, viewer.Entity, target.Entity))
        {
            return CreateErrorView(Lang.Get("thebasics:charsheet-error-look-los"));
        }

        return BuildSheetViewMessage(viewer, target, CharacterSheetViewMode.Look, includeEmpty: false, canEdit: false, isAdminView: false);
    }

    private CharacterSheetViewMessage BuildSheetViewMessage(IServerPlayer viewer, IServerPlayer target, CharacterSheetViewMode mode, bool includeEmpty, bool canEdit, bool isAdminView)
    {
        var data = GetSheetData(target);
        var displayName = GetCharacterDisplayName(target);
        var view = new CharacterSheetViewMessage
        {
            Title = GetSheetTitle(viewer, target, mode, displayName),
            TargetPlayerUid = target.PlayerUID,
            TargetPlayerName = target.PlayerName,
            DisplayName = displayName,
            CanEdit = canEdit,
            IsAdminView = isAdminView,
            IsLookView = mode == CharacterSheetViewMode.Look,
            Headshot = Config.EnableCharacterHeadshots ? data.Headshot : null,
            CanEditHeadshot = Config.EnableCharacterHeadshots && canEdit
        };

        foreach (var field in Config.CharacterSheetFields)
        {
            if (!CanViewField(viewer, target, field, mode))
            {
                continue;
            }

            var value = GetFieldValue(target, data, field, Config);
            if (!includeEmpty && string.IsNullOrWhiteSpace(value) && field.Optional)
            {
                continue;
            }

            view.Fields.Add(CreateFieldView(viewer, target, field, value, canEdit, isAdminView));
        }

        if (view.Fields.Count == 0 && view.Headshot == null)
        {
            view.Message = mode == CharacterSheetViewMode.Look ? Lang.Get("thebasics:charsheet-look-empty") : Lang.Get("thebasics:charsheet-empty");
        }

        return view;
    }

    private CharacterSheetFieldViewMessage CreateFieldView(IServerPlayer viewer, IServerPlayer target, CharacterSheetFieldDefinition field, string value, bool canEdit, bool isAdminView)
    {
        return new CharacterSheetFieldViewMessage
        {
            FieldId = GetFieldId(field),
            Label = GetFieldLabel(field),
            Type = GetFieldType(field),
            Value = value,
            Optional = field.Optional,
            MaxLength = GetMaxLength(field),
            Options = field.Options?.ToList() ?? new List<string>(),
            CanEdit = canEdit && CanEditField(viewer, target, field, isAdminView),
            Visibility = GetFieldVisibility(field),
            EditorRows = GetEditorRows(field),
            LayoutSection = ResolveLayoutSection(field),
            Width = CharacterSheetFieldWidths.Normalize(field.Width)
        };
    }

    /// <summary>
    /// Returns the field's configured LayoutSection, with backfill: configs predating layout sections
    /// won't have one set, so we infer HeaderSide for nickname/full-name fields and Body for everything
    /// else. This means existing servers see the new layout for their name fields without any config edit.
    /// </summary>
    private static string ResolveLayoutSection(CharacterSheetFieldDefinition field)
    {
        if (!string.IsNullOrWhiteSpace(field.LayoutSection))
        {
            return CharacterSheetLayoutSections.Normalize(field.LayoutSection);
        }

        return IsNicknameField(field) || IsFullNameField(field)
            ? CharacterSheetLayoutSections.HeaderSide
            : CharacterSheetLayoutSections.Body;
    }

    private static string GetSheetTitle(IServerPlayer viewer, IServerPlayer target, CharacterSheetViewMode mode, string displayName)
    {
        if (mode == CharacterSheetViewMode.Look)
        {
            return Lang.Get("thebasics:charsheet-look-header", displayName);
        }

        return viewer != null && viewer.PlayerUID == target.PlayerUID
            ? Lang.Get("thebasics:charsheet-header-own", displayName)
            : Lang.Get("thebasics:charsheet-header-other", displayName);
    }

    private string RenderSheet(IServerPlayer viewer, IServerPlayer target, CharacterSheetViewMode mode, bool includeEmpty)
    {
        var isOwnSheet = viewer != null && viewer.PlayerUID == target.PlayerUID;
        var data = GetSheetData(target);
        var displayName = GetCharacterDisplayName(target);
        var header = Lang.Get("thebasics:charsheet-header-other", VtmlUtils.EscapeVtml(displayName));
        if (mode == CharacterSheetViewMode.Look)
        {
            header = Lang.Get("thebasics:charsheet-look-header", VtmlUtils.EscapeVtml(displayName));
        }
        else if (isOwnSheet)
        {
            header = Lang.Get("thebasics:charsheet-header-own", VtmlUtils.EscapeVtml(displayName));
        }

        var message = new StringBuilder(header);

        var renderedFields = AppendVisibleFields(message, viewer, target, data, mode, includeEmpty);

        if (renderedFields == 0)
        {
            message.Append('\n');
            message.Append(mode == CharacterSheetViewMode.Look ? Lang.Get("thebasics:charsheet-look-empty") : Lang.Get("thebasics:charsheet-empty"));
        }

        return message.ToString();
    }

    private int AppendVisibleFields(StringBuilder message, IServerPlayer viewer, IServerPlayer target, CharacterSheetData data, CharacterSheetViewMode mode, bool includeEmpty)
    {
        var renderedFields = 0;
        foreach (var field in Config.CharacterSheetFields)
        {
            if (!CanViewField(viewer, target, field, mode))
            {
                continue;
            }

            var value = GetFieldValue(target, data, field, Config);
            if (!includeEmpty && string.IsNullOrWhiteSpace(value) && field.Optional)
            {
                continue;
            }

            renderedFields++;
            message.Append('\n');
            message.Append(VtmlUtils.EscapeVtml(GetFieldLabel(field)));
            message.Append(": ");
            message.Append(string.IsNullOrWhiteSpace(value) ? Lang.Get("thebasics:charsheet-unset") : VtmlUtils.EscapeVtml(value));
        }

        return renderedFields;
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
            message.Append(", ");
            message.Append(VtmlUtils.EscapeVtml(GetFieldVisibility(field)));
            if (IsNicknameField(field))
            {
                message.Append(", nickname");
            }
            if (IsFullNameField(field))
            {
                message.Append(", full name");
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
        return SetField(player, player, args.Parsers[0].GetValue()?.ToString(), args.Parsers[1].GetValue()?.ToString(), isAdminAction: false);
    }

    private TextCommandResult ClearOwnField(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        return ClearField(player, player, args.Parsers[0].IsMissing ? null : args.Parsers[0].GetValue()?.ToString(), isAdminAction: false);
    }

    private TextCommandResult SetAdminField(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var editor = args.Caller.Type == EnumCallerType.Player ? args.Caller.Player as IServerPlayer : null;
        return SetField(editor, target, args.Parsers[1].GetValue()?.ToString(), args.Parsers[2].GetValue()?.ToString(), isAdminAction: true);
    }

    private TextCommandResult ClearAdminField(TextCommandCallingArgs args)
    {
        var target = ResolveTarget(args, 0, allowSelfFallback: false);
        if (target == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-player-not-found"));
        }

        var editor = args.Caller.Type == EnumCallerType.Player ? args.Caller.Player as IServerPlayer : null;
        return ClearField(editor, target, args.Parsers[1].IsMissing ? null : args.Parsers[1].GetValue()?.ToString(), isAdminAction: true);
    }

    private TextCommandResult SetField(IServerPlayer editor, IServerPlayer player, string fieldName, string value, bool isAdminAction)
    {
        var field = ResolveField(fieldName);
        if (field == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-not-found", fieldName, GetValidFieldList()));
        }

        if (!CanEditField(editor, player, field, isAdminAction))
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-readonly", GetFieldLabel(field)));
        }

        value = (value ?? string.Empty).Trim();
        if (!TryNormalizeValue(field, value, out var normalizedValue, out var errorMessage))
        {
            return Error(errorMessage);
        }

        if (!TrySetFieldValue(player, field, normalizedValue, out errorMessage))
        {
            return Error(errorMessage);
        }

        API.Logger.Audit($"{(isAdminAction ? "Admin" : "Player")} updated character sheet field '{GetFieldId(field)}' for {player.PlayerName}.");
        return Success(Lang.Get(
            "thebasics:charsheet-success-set",
            VtmlUtils.EscapeVtml(GetFieldLabel(field)),
            VtmlUtils.EscapeVtml(player.PlayerName),
            VtmlUtils.EscapeVtml(normalizedValue)));
    }

    private TextCommandResult ClearField(IServerPlayer editor, IServerPlayer player, string fieldName, bool isAdminAction)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            foreach (var field in Config.CharacterSheetFields.Where(field => CanEditField(editor, player, field, isAdminAction)).ToArray())
            {
                ClearFieldValue(player, field);
            }

            API.Logger.Audit($"Character sheet cleared for {player.PlayerName}.");
            return Success(Lang.Get("thebasics:charsheet-success-cleared-all", VtmlUtils.EscapeVtml(player.PlayerName)));
        }

        var resolvedField = ResolveField(fieldName);
        if (resolvedField == null)
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-not-found", fieldName, GetValidFieldList()));
        }

        if (!CanEditField(editor, player, resolvedField, isAdminAction))
        {
            return Error(Lang.Get("thebasics:charsheet-error-field-readonly", GetFieldLabel(resolvedField)));
        }

        ClearFieldValue(player, resolvedField);

        API.Logger.Audit($"Character sheet field '{GetFieldId(resolvedField)}' cleared for {player.PlayerName}.");
        return Success(Lang.Get("thebasics:charsheet-success-cleared-field", VtmlUtils.EscapeVtml(GetFieldLabel(resolvedField)), VtmlUtils.EscapeVtml(player.PlayerName)));
    }

    private bool TrySetFieldValue(IServerPlayer player, CharacterSheetFieldDefinition field, string normalizedValue, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (IsNicknameField(field))
        {
            if (!NicknameValidationUtils.ValidateNickname(player, normalizedValue, API, Config, out var conflictingPlayer, out var conflictType))
            {
                errorMessage = Lang.Get("thebasics:chat-nick-conflict", normalizedValue, conflictingPlayer, conflictType);
                return false;
            }

            player.SetNickname(normalizedValue, Config);
            RefreshNameTag(player);
            return true;
        }

        var data = GetSheetData(player);
        SetStoredFieldValue(data, GetFieldId(field), normalizedValue);
        SaveSheetData(player, data);
        if (IsFullNameField(field))
        {
            RefreshNameTag(player);
        }
        return true;
    }

    private void ClearFieldValue(IServerPlayer player, CharacterSheetFieldDefinition field)
    {
        if (IsNicknameField(field))
        {
            player.ClearNickname(Config);
            RefreshNameTag(player);
            return;
        }

        var data = GetSheetData(player);
        RemoveStoredField(data, GetFieldId(field));
        SaveSheetData(player, data);
        if (IsFullNameField(field))
        {
            RefreshNameTag(player);
        }
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

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
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

    private IServerPlayer ResolveLookTarget(TextCommandCallingArgs args, IServerPlayer viewer)
    {
        if (!args.Parsers[0].IsMissing)
        {
            return ResolveTarget(args, 0, allowSelfFallback: false);
        }

        return ResolveSelectedPlayer(viewer);
    }

    private IServerPlayer ResolveSelectedPlayer(IServerPlayer viewer)
    {
        if (viewer.CurrentEntitySelection?.Entity is not EntityPlayer selectedPlayer)
        {
            return null;
        }

        return API.World.PlayerByUid(selectedPlayer.PlayerUID) as IServerPlayer;
    }

    private IServerPlayer ResolveNetworkTarget(CharacterSheetOpenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request?.TargetPlayerUid))
        {
            return API.GetPlayerByUID(request.TargetPlayerUid);
        }

        if (string.IsNullOrWhiteSpace(request?.TargetPlayerName))
        {
            return null;
        }

        var targetName = request.TargetPlayerName.Trim();
        return API.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .FirstOrDefault(player => player.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                                      player.PlayerUID.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                                      player.GetNickname(Config).Equals(targetName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanViewField(IServerPlayer viewer, IServerPlayer target, CharacterSheetFieldDefinition field, CharacterSheetViewMode mode)
    {
        if (mode == CharacterSheetViewMode.Admin)
        {
            return true;
        }

        var visibility = GetFieldVisibility(field);
        if (mode == CharacterSheetViewMode.Look)
        {
            return field.ShowInLook && (visibility == CharacterSheetFieldVisibilities.Public || visibility == CharacterSheetFieldVisibilities.Nearby);
        }

        if (viewer != null && viewer.PlayerUID == target.PlayerUID)
        {
            return visibility != CharacterSheetFieldVisibilities.Admin;
        }

        return visibility == CharacterSheetFieldVisibilities.Public;
    }

    private static bool CanEditField(IServerPlayer viewer, IServerPlayer target, CharacterSheetFieldDefinition field, bool isAdminAction)
    {
        if (isAdminAction)
        {
            return true;
        }

        return viewer != null && target != null && viewer.PlayerUID == target.PlayerUID && GetFieldVisibility(field) != CharacterSheetFieldVisibilities.Admin;
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

    public static string[] GetMissingRequiredFieldLabels(IServerPlayer player, ModConfig config)
    {
        if (player == null || config?.EnableCharacterSheets != true || !config.CharacterSheetRequireRequiredFieldsForRoleplay)
        {
            return Array.Empty<string>();
        }

        return config.CharacterSheetFields?
            .Where(field => FieldCanBeRequiredForRoleplay(field, config))
            .Where(field => string.IsNullOrWhiteSpace(GetFieldValue(player, GetSheetData(player), field, config)))
            .Select(GetFieldLabel)
            .ToArray() ?? Array.Empty<string>();
    }

    private static bool FieldCanBeRequiredForRoleplay(CharacterSheetFieldDefinition field, ModConfig config)
    {
        if (field?.Optional != false)
        {
            return false;
        }

        if (IsNicknameField(field) && config.DisableNicknames)
        {
            return false;
        }

        return GetFieldVisibility(field) != CharacterSheetFieldVisibilities.Admin;
    }

    private static CharacterSheetData GetSheetData(IServerPlayer player)
    {
        var data = IServerPlayerExtensions.GetModData(player, ModDataKey, new CharacterSheetData()) ?? new CharacterSheetData();
        data.Fields ??= new List<CharacterSheetStoredField>();
        return data;
    }

    private static void SaveSheetData(IServerPlayer player, CharacterSheetData data)
    {
        IServerPlayerExtensions.SetModData(player, ModDataKey, data);
    }

    private static string GetFieldValue(IServerPlayer player, CharacterSheetData data, CharacterSheetFieldDefinition field, ModConfig config)
    {
        if (IsNicknameField(field))
        {
            return player.HasNickname(config) ? player.GetNickname(config) : string.Empty;
        }

        return data.Fields.FirstOrDefault(storedField => storedField.FieldId.Equals(GetFieldId(field), StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
    }

    private string GetCharacterDisplayName(IServerPlayer player)
    {
        var fullName = player.GetCharacterSheetFullName(Config)?.Trim();
        var nickname = player.HasNickname(Config) ? player.GetNickname(Config)?.Trim() : string.Empty;

        if (!string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(nickname) && !fullName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
        {
            return $"{fullName} ({nickname})";
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return string.IsNullOrWhiteSpace(nickname) ? player.PlayerName : nickname;
    }

    private static void SetStoredFieldValue(CharacterSheetData data, string fieldId, string value)
    {
        var storedField = data.Fields.FirstOrDefault(field => field.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
        if (storedField == null)
        {
            data.Fields.Add(new CharacterSheetStoredField
            {
                FieldId = fieldId,
                Value = value
            });
            return;
        }

        storedField.Value = value;
    }

    private static void RemoveStoredField(CharacterSheetData data, string fieldId)
    {
        for (var index = data.Fields.Count - 1; index >= 0; index--)
        {
            if (data.Fields[index].FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase))
            {
                data.Fields.RemoveAt(index);
            }
        }
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

    private static int GetEditorRows(CharacterSheetFieldDefinition field)
    {
        return GetFieldType(field) == CharacterSheetFieldTypes.LongString ? Math.Clamp(field.EditorRows, 0, 16) : 0;
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

    private static string GetFieldVisibility(CharacterSheetFieldDefinition field)
    {
        var visibility = string.IsNullOrWhiteSpace(field.Visibility) ? CharacterSheetFieldVisibilities.Public : field.Visibility.Trim().ToLowerInvariant();
        return visibility is CharacterSheetFieldVisibilities.Public
            or CharacterSheetFieldVisibilities.Nearby
            or CharacterSheetFieldVisibilities.Self
            or CharacterSheetFieldVisibilities.Admin
            ? visibility
            : CharacterSheetFieldVisibilities.Public;
    }

    private static bool IsNicknameField(CharacterSheetFieldDefinition field)
    {
        return field.BindTo?.Equals(NicknameBind, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsFullNameField(CharacterSheetFieldDefinition field)
    {
        return field.BindTo?.Equals(FullNameBind, StringComparison.OrdinalIgnoreCase) == true;
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
        if (player.Entity == null)
        {
            return;
        }

        var proximityChatSystem = API.ModLoader.GetModSystem<ProximityChat.RPProximityChatSystem>();
        if (proximityChatSystem != null)
        {
            proximityChatSystem.RefreshPlayerIdentityState(player);
            return;
        }

        RefreshEntityNameTag(player);
    }

    private void RefreshEntityNameTag(IServerPlayer player)
    {
        var behavior = player.Entity.GetBehavior<EntityBehaviorNameTag>();
        if (behavior == null)
        {
            return;
        }

        behavior.ShowOnlyWhenTargeted = Config.HideNametagUnlessTargeting;
        behavior.RenderRange = Config.NametagRenderRange;
        behavior.SetName(BuildNametagDisplayName(player, Config));
        SyncNicknameColorAttr(player);
    }

    /// <summary>
    /// Pushes the player's current nickname color into the "nametag" tree-attribute so the
    /// client-side patched nametag renderer can apply it to the name text. Stored next to the
    /// headshot hash so vanilla's OnNameChanged listener picks up changes here too.
    /// </summary>
    internal static void SyncNicknameColorAttr(IServerPlayer player)
    {
        if (player?.Entity?.WatchedAttributes is not { } attrs) return;
        var nametag = attrs.GetTreeAttribute(NametagAttrTree);
        if (nametag == null) return;
        var color = player.GetNicknameColor() ?? string.Empty;
        if (nametag.GetString(NicknameColorAttrKey) == color) return;
        nametag.SetString(NicknameColorAttrKey, color);
        attrs.MarkPathDirty(NametagAttrTree);
    }

    internal static string BuildNametagDisplayName(IServerPlayer player, ModConfig config)
    {
        // Returns plain text. Coloring is applied client-side by EntityBehaviorNameTagPatches
        // using the separate `thebasicsNicknameColor` watched-attribute, so any vanilla code path
        // that reads the nametag name (look-at HUD, character dialog title, etc.) shows clean text.
        if (player == null || config == null)
        {
            return string.Empty;
        }

        if (!config.ShowNicknameInNametag)
        {
            return config.ShowPlayerNameInNametag ? player.PlayerName : string.Empty;
        }

        string displayName = null;
        if (player.HasNickname(config))
        {
            displayName = player.GetNickname(config)?.Trim();
        }
        else if (config.EnableCharacterSheets)
        {
            displayName = player.GetCharacterSheetFullName(config)?.Trim();
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return config.ShowPlayerNameInNametag ? player.PlayerName : string.Empty;
        }

        if (!config.ShowPlayerNameInNametag || displayName.Equals(player.PlayerName, StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        return $"{displayName} ({player.PlayerName})";
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

    private static CharacterSheetViewMessage CreateErrorView(string message, string errorCode = "")
    {
        return new CharacterSheetViewMessage
        {
            Success = false,
            IsErrorResponse = true,
            Message = message,
            ErrorCode = errorCode ?? string.Empty
        };
    }
}

internal enum CharacterSheetViewMode
{
    Full,
    Look,
    Admin
}
