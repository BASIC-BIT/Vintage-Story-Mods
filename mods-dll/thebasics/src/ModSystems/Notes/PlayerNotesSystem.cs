#pragma warning disable S1541, S3776 // Command/network orchestration needs behavior-preserving refactors, not opportunistic churn.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.Notes.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.RpCharacters;
using thebasics.ModSystems.RpCharacters.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Notes;

public class PlayerNotesSystem : BaseBasicModSystem
{
    private const int MaxNoteTitleLength = 120;

    private PlayerNotesStore _store;

    protected override void BasicStartServerSide()
    {
        _store = new PlayerNotesStore(API);
        RegisterCommands();
    }

    protected override void OnConfigReloaded(IReadOnlySet<string> changedKeys)
    {
        if (changedKeys.Contains(nameof(Config.AdminNotesPermission)))
        {
            API.ChatCommands.Get("adminnotes")?.RequiresPrivilege(Config.AdminNotesPermission);
        }

        if (changedKeys.Contains(nameof(Config.PlayerNotesPermission)))
        {
            API.ChatCommands.Get("notes")?.RequiresPrivilege(Config.PlayerNotesPermission);
        }
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("adminnotes")
            .WithAlias("anotes", "staffnotes")
            .WithDescription(Lang.Get("thebasics:notes-cmd-admin-desc"))
            .RequiresPrivilege(Config.AdminNotesPermission)
            .WithArgs(new StringArgParser("arguments", false))
            .HandleWith(HandleAdminNotesCommand);

        API.ChatCommands.GetOrCreate("notes")
            .WithAlias("personalnotes", "pnotes")
            .WithDescription(Lang.Get("thebasics:notes-cmd-personal-desc"))
            .RequiresPrivilege(Config.PlayerNotesPermission)
            .RequiresPlayer()
            .WithArgs(new StringArgParser("arguments", false))
            .HandleWith(HandlePersonalNotesCommand);
    }

    public TheBasicsNotesViewMessage HandleNotesOpenRequest(IServerPlayer actor, TheBasicsNotesOpenRequest request)
    {
        var scope = NormalizeScope(request?.Scope);
        if (scope == PlayerNotesConstants.ScopeAdmin)
        {
            if (!CanUseAdminNotes(actor, allowConsole: false))
            {
                return ErrorView(scope, Lang.Get("thebasics:notes-error-admin-permission"));
            }

            if (!TryResolveTarget(request?.TargetPlayerUid, request?.TargetQuery, allowOffline: true, out var target, out var error))
            {
                return ErrorView(scope, error);
            }

            return BuildAdminView(actor, target, Lang.Get("thebasics:notes-status-reloaded"));
        }

        if (actor == null || !CanUsePersonalNotes(actor))
        {
            return ErrorView(scope, Lang.Get("thebasics:notes-error-personal-permission"));
        }

        var personalTarget = ResolvePersonalTarget(actor, request?.TargetPlayerUid, request?.TargetQuery, out var personalError);
        return personalTarget == null
            ? ErrorView(scope, personalError)
            : BuildPersonalView(actor, personalTarget, Lang.Get("thebasics:notes-status-reloaded"));
    }

    public TheBasicsNotesViewMessage HandleNotesSaveRequest(IServerPlayer actor, TheBasicsNotesSaveMessage message)
    {
        var scope = NormalizeScope(message?.Scope);
        if (message?.Reload == true)
        {
            return HandleNotesOpenRequest(actor, new TheBasicsNotesOpenRequest
            {
                Scope = scope,
                TargetPlayerUid = message.TargetPlayerUid,
                TargetQuery = message.TargetQuery
            });
        }

        if (scope == PlayerNotesConstants.ScopeAdmin)
        {
            return SaveAdminView(actor, message);
        }

        return SavePersonalView(actor, message);
    }

    private TextCommandResult HandleAdminNotesCommand(TextCommandCallingArgs args)
    {
        if (!CanUseAdminNotes(args.Caller.Player as IServerPlayer))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-admin-disabled"));
        }

        var raw = GetRawArgument(args);
        var targetQuery = PopToken(ref raw);
        if (string.IsNullOrWhiteSpace(targetQuery))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-admin-usage"));
        }

        if (!TryResolveTarget(null, targetQuery, allowOffline: true, out var target, out var error))
        {
            return TextCommandResult.Error(error);
        }

        var action = PopToken(ref raw).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "open")
        {
            if (args.Caller.Player is not IServerPlayer player)
            {
                return TextCommandResult.Error(Lang.Get("thebasics:notes-error-player-required"));
            }

            PushNotesView(player, BuildAdminView(player, target, Lang.Get("thebasics:notes-status-opened")));
            return TextCommandResult.Success(Lang.Get("thebasics:notes-open-admin", target.Name));
        }

        if (IsStructuredAdminAction(action) && !CanUseStructuredAdminNotes(args.Caller.Player as IServerPlayer))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-structured-admin-disabled"));
        }

        return action switch
        {
            "list" => TextCommandResult.Success(RenderNoteList(GetAdminNotes(target.Uid), Lang.Get("thebasics:notes-admin-list-empty", target.Name))),
            "add" => AddAdminNote(args.Caller.Player as IServerPlayer, target, raw),
            "view" => ViewAdminNote(target, PopToken(ref raw)),
            "edit" => EditAdminNote(args.Caller.Player as IServerPlayer, target, PopToken(ref raw), raw),
            "delete" => DeleteAdminNote(args.Caller.Player as IServerPlayer, target, PopToken(ref raw), PopToken(ref raw)),
            "ledger" => HandleLedgerCommand(args.Caller.Player as IServerPlayer, target, raw),
            _ => TextCommandResult.Error(Lang.Get("thebasics:notes-error-unknown-action", action))
        };
    }

    private TextCommandResult HandlePersonalNotesCommand(TextCommandCallingArgs args)
    {
        var actor = args.Caller.Player as IServerPlayer;
        if (actor == null || !CanUsePersonalNotes(actor))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-personal-disabled"));
        }

        var raw = GetRawArgument(args);
        PlayerNoteTarget target;
        var action = PopToken(ref raw).ToLowerInvariant();

        if (action == "about")
        {
            var targetQuery = PopToken(ref raw);
            target = ResolvePersonalTarget(actor, null, targetQuery, out var targetError);
            if (target == null)
            {
                return TextCommandResult.Error(targetError);
            }

            action = PopToken(ref raw).ToLowerInvariant();
        }
        else
        {
            target = PlayerNoteTarget.FromPlayer(actor, GetActiveCharacter(actor));
        }

        if (string.IsNullOrWhiteSpace(action) || action == "open")
        {
            PushNotesView(actor, BuildPersonalView(actor, target, Lang.Get("thebasics:notes-status-opened")));
            return TextCommandResult.Success(Lang.Get("thebasics:notes-open-personal", target.Name));
        }

        return action switch
        {
            "list" => TextCommandResult.Success(RenderNoteList(GetPersonalNotes(actor.PlayerUID, target.Uid), Lang.Get("thebasics:notes-personal-list-empty", target.Name))),
            "add" => AddPersonalNote(actor, target, raw),
            "view" => ViewPersonalNote(actor, target, PopToken(ref raw)),
            "edit" => EditPersonalNote(actor, target, PopToken(ref raw), raw),
            "delete" => DeletePersonalNote(actor, target, PopToken(ref raw), PopToken(ref raw)),
            _ => TextCommandResult.Error(Lang.Get("thebasics:notes-error-unknown-action", action))
        };
    }

    private TheBasicsNotesViewMessage SaveAdminView(IServerPlayer actor, TheBasicsNotesSaveMessage message)
    {
        if (!CanUseAdminNotes(actor, allowConsole: false))
        {
            return ErrorView(PlayerNotesConstants.ScopeAdmin, Lang.Get("thebasics:notes-error-admin-permission"));
        }

        if (!TryResolveTarget(message?.TargetPlayerUid, message?.TargetQuery, allowOffline: true, out var target, out var error))
        {
            return ErrorView(PlayerNotesConstants.ScopeAdmin, error);
        }

        var data = _store.Load();
        var summary = new MutationSummary();
        if (Config.EnableStructuredAdminNotes &&
            !ReplaceNotes(new NoteReplacement(data.AdminNotes, message?.AdminNotes, PlayerNotesConstants.KindAdmin, target, Config.MaxAdminNotesPerTarget, 0), actor, summary, out error))
        {
            return BuildAdminView(actor, target, error, success: false, data);
        }

        if (Config.EnableAdminNoteLedger &&
            !SetLedger(data, actor, target, message?.AdminLedger?.Text ?? string.Empty, summary, out error))
        {
            return BuildAdminView(actor, target, error, success: false, data);
        }

        if (!_store.Save(data))
        {
            return BuildAdminView(actor, target, Lang.Get("thebasics:notes-error-save-failed"), success: false, data);
        }

        if (summary.HasChanges)
        {
            API.Logger.Audit($"Admin {ActorLabel(actor)} saved notes for {target.Name} ({target.Uid}): +{summary.Added}/~{summary.Updated}/-{summary.Deleted}; freeformChanged={summary.FreeformChanged}.");
        }

        return BuildAdminView(actor, target, Lang.Get("thebasics:notes-save-success"), data: data);
    }

    private TheBasicsNotesViewMessage SavePersonalView(IServerPlayer actor, TheBasicsNotesSaveMessage message)
    {
        if (actor == null || !CanUsePersonalNotes(actor))
        {
            return ErrorView(PlayerNotesConstants.ScopePersonal, Lang.Get("thebasics:notes-error-personal-permission"));
        }

        var target = ResolvePersonalTarget(actor, message?.TargetPlayerUid, message?.TargetQuery, out var error);
        if (target == null)
        {
            return ErrorView(PlayerNotesConstants.ScopePersonal, error);
        }

        var data = _store.Load();
        var otherPersonalCount = CountPersonalNotesOutsideTarget(data, actor.PlayerUID, target.Uid);
        var summary = new MutationSummary();
        if (!ReplaceNotes(new NoteReplacement(data.PersonalNotes, message?.PersonalNotes, PlayerNotesConstants.KindPersonal, target, Config.MaxPlayerNotesPerAuthor, otherPersonalCount), actor, summary, out error))
        {
            return BuildPersonalView(actor, target, error, success: false, data);
        }

        if (!SetPersonalLedger(data, actor, target, message?.PersonalLedger?.Text ?? string.Empty, summary, out error))
        {
            return BuildPersonalView(actor, target, error, success: false, data);
        }

        if (!_store.Save(data))
        {
            return BuildPersonalView(actor, target, Lang.Get("thebasics:notes-error-save-failed"), success: false, data);
        }

        return BuildPersonalView(actor, target, Lang.Get("thebasics:notes-save-success"), data: data);
    }

    private TheBasicsNotesViewMessage BuildAdminView(IServerPlayer viewer, PlayerNoteTarget target, string message, bool success = true, PlayerNotesData data = null)
    {
        data ??= _store.Load();
        var showNotes = Config.EnableAdminNotes && Config.EnableStructuredAdminNotes;
        var showLedger = Config.EnableAdminNotes && Config.EnableAdminNoteLedger;
        return new TheBasicsNotesViewMessage
        {
            Success = success,
            Message = message ?? string.Empty,
            Scope = PlayerNotesConstants.ScopeAdmin,
            TargetPlayerUid = target.Uid,
            TargetPlayerName = target.Name,
            ShowAdminNotes = showNotes,
            ShowAdminLedger = showLedger,
            CanEditAdminNotes = showNotes && viewer?.HasPrivilege(Config.AdminNotesPermission) == true,
            CanEditAdminLedger = showLedger && viewer?.HasPrivilege(Config.AdminNotesPermission) == true,
            AdminNotes = showNotes ? GetAdminNotes(target.Uid, data).Select(CloneNote).ToList() : new List<PlayerNoteEntryMessage>(),
            AdminLedger = showLedger ? CloneLedger(GetLedger(target, data)) : new AdminNoteLedgerMessage(),
            MaxNoteLength = Config.MaxNoteLength,
            MaxFreeformNoteLength = Config.MaxFreeformNoteLength
        };
    }

    private TheBasicsNotesViewMessage BuildPersonalView(IServerPlayer viewer, PlayerNoteTarget target, string message, bool success = true, PlayerNotesData data = null)
    {
        data ??= _store.Load();
        var showPersonal = Config.EnablePlayerNotes;
        return new TheBasicsNotesViewMessage
        {
            Success = success,
            Message = message ?? string.Empty,
            Scope = PlayerNotesConstants.ScopePersonal,
            TargetPlayerUid = target.Uid,
            TargetPlayerName = target.Name,
            ShowPersonalNotes = showPersonal,
            CanEditPersonalNotes = showPersonal && viewer?.HasPrivilege(Config.PlayerNotesPermission) == true,
            PersonalNotes = showPersonal ? GetPersonalNotes(viewer.PlayerUID, target.Uid, data).Select(CloneNote).ToList() : new List<PlayerNoteEntryMessage>(),
            PersonalLedger = showPersonal ? ClonePersonalLedger(GetPersonalLedger(viewer.PlayerUID, target, data)) : new PersonalNoteLedgerMessage(),
            MaxNoteLength = Config.MaxNoteLength,
            MaxFreeformNoteLength = Config.MaxFreeformNoteLength
        };
    }

    private TextCommandResult AddAdminNote(IServerPlayer actor, PlayerNoteTarget target, string text)
    {
        return MutateSingleNote(actor, target, PlayerNotesConstants.KindAdmin, null, text, MutationKind.Add);
    }

    private TextCommandResult EditAdminNote(IServerPlayer actor, PlayerNoteTarget target, string id, string text)
    {
        return MutateSingleNote(actor, target, PlayerNotesConstants.KindAdmin, id, text, MutationKind.Edit);
    }

    private TextCommandResult DeleteAdminNote(IServerPlayer actor, PlayerNoteTarget target, string id, string confirm)
    {
        if (!string.Equals(confirm, "confirm", StringComparison.OrdinalIgnoreCase))
        {
            return TextCommandResult.Success(Lang.Get("thebasics:notes-delete-confirm", id));
        }

        return MutateSingleNote(actor, target, PlayerNotesConstants.KindAdmin, id, string.Empty, MutationKind.Delete);
    }

    private TextCommandResult ViewAdminNote(PlayerNoteTarget target, string id)
    {
        var notes = GetAdminNotes(target.Uid);
        return TryResolveNote(notes, id, out var note, out var error)
            ? TextCommandResult.Success(RenderNote(note))
            : TextCommandResult.Error(error);
    }

    private TextCommandResult AddPersonalNote(IServerPlayer actor, PlayerNoteTarget target, string text)
    {
        return MutateSingleNote(actor, target, PlayerNotesConstants.KindPersonal, null, text, MutationKind.Add);
    }

    private TextCommandResult EditPersonalNote(IServerPlayer actor, PlayerNoteTarget target, string id, string text)
    {
        return MutateSingleNote(actor, target, PlayerNotesConstants.KindPersonal, id, text, MutationKind.Edit);
    }

    private TextCommandResult DeletePersonalNote(IServerPlayer actor, PlayerNoteTarget target, string id, string confirm)
    {
        if (!string.Equals(confirm, "confirm", StringComparison.OrdinalIgnoreCase))
        {
            return TextCommandResult.Success(Lang.Get("thebasics:notes-delete-confirm", id));
        }

        return MutateSingleNote(actor, target, PlayerNotesConstants.KindPersonal, id, string.Empty, MutationKind.Delete);
    }

    private TextCommandResult ViewPersonalNote(IServerPlayer actor, PlayerNoteTarget target, string id)
    {
        var notes = GetPersonalNotes(actor.PlayerUID, target.Uid);
        return TryResolveNote(notes, id, out var note, out var error)
            ? TextCommandResult.Success(RenderNote(note))
            : TextCommandResult.Error(error);
    }

    private TextCommandResult MutateSingleNote(IServerPlayer actor, PlayerNoteTarget target, string kind, string id, string text, MutationKind mutation)
    {
        if (actor == null)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-player-required"));
        }

        if (mutation != MutationKind.Delete && !ValidateNoteText(text, out var error))
        {
            return TextCommandResult.Error(error);
        }

        var data = _store.Load();
        var list = kind == PlayerNotesConstants.KindAdmin ? data.AdminNotes : data.PersonalNotes;
        var scopedNotes = list.Where(candidate => BelongsToScopeTarget(candidate, kind, actor.PlayerUID, target.Uid)).ToList();
        var note = mutation == MutationKind.Add ? null : ResolveNote(scopedNotes, id);
        if (mutation != MutationKind.Add && note == null)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-note-not-found", id));
        }

        switch (mutation)
        {
            case MutationKind.Add:
                if (!CanAddNote(data, kind, actor.PlayerUID, target.Uid, out error))
                {
                    return TextCommandResult.Error(error);
                }

                note = CreateNote(kind, actor, target, text);
                list.Add(note);
                break;
            case MutationKind.Edit:
                note.Text = NormalizeText(text);
                note.UpdatedUtc = NowUtc();
                break;
            case MutationKind.Delete:
                list.Remove(note);
                break;
        }

        if (!_store.Save(data))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-save-failed"));
        }

        if (kind == PlayerNotesConstants.KindAdmin)
        {
            API.Logger.Audit($"Admin {ActorLabel(actor)} {mutation.ToString().ToLowerInvariant()} admin note {note?.Id ?? id} for {target.Name} ({target.Uid}).");
        }

        return TextCommandResult.Success(Lang.Get("thebasics:notes-command-success", mutation.ToString().ToLowerInvariant(), target.Name));
    }

    private TextCommandResult HandleLedgerCommand(IServerPlayer actor, PlayerNoteTarget target, string raw)
    {
        if (!Config.EnableAdminNoteLedger)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-ledger-disabled"));
        }

        var action = PopToken(ref raw).ToLowerInvariant();
        var data = _store.Load();
        if (string.IsNullOrWhiteSpace(action) || action == "view")
        {
            var ledger = GetLedger(target, data);
            return TextCommandResult.Success(string.IsNullOrWhiteSpace(ledger.Text)
                ? Lang.Get("thebasics:notes-ledger-empty", target.Name)
                : VtmlUtils.EscapeVtml(ledger.Text));
        }

        if (action != "set")
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-unknown-action", "ledger " + action));
        }

        var summary = new MutationSummary();
        if (!SetLedger(data, actor, target, raw, summary, out var error))
        {
            return TextCommandResult.Error(error);
        }

        if (!_store.Save(data))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:notes-error-save-failed"));
        }

        if (summary.FreeformChanged)
        {
            API.Logger.Audit($"Admin {ActorLabel(actor)} updated freeform staff notes for {target.Name} ({target.Uid}); length={NormalizeText(raw).Length}.");
        }

        return TextCommandResult.Success(Lang.Get("thebasics:notes-ledger-save-success", target.Name));
    }

    private bool ReplaceNotes(NoteReplacement replacement, IServerPlayer actor, MutationSummary summary, out string error)
    {
        error = null;
        var submittedList = (replacement.Submitted ?? Array.Empty<PlayerNoteEntryMessage>()).Where(note => note != null).ToList();
        if (replacement.CountOutsideTarget + submittedList.Count > replacement.MaxCount)
        {
            error = Lang.Get("thebasics:notes-error-too-many", replacement.MaxCount);
            return false;
        }

        var existing = replacement.List.Where(note => BelongsToScopeTarget(note, replacement.Kind, actor.PlayerUID, replacement.Target.Uid)).ToList();
        var existingById = existing.Where(note => !string.IsNullOrWhiteSpace(note.Id)).ToDictionary(note => note.Id, StringComparer.OrdinalIgnoreCase);
        var replacements = new List<PlayerNoteEntryMessage>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var submittedNote in submittedList)
        {
            if (!ValidateNoteEntry(submittedNote.Title, submittedNote.Text, out error))
            {
                return false;
            }

            var note = existingById.TryGetValue(submittedNote.Id ?? string.Empty, out var matched)
                ? CloneNote(matched)
                : CreateNote(replacement.Kind, actor, replacement.Target, submittedNote.Text);
            if (!seenIds.Add(note.Id))
            {
                error = Lang.Get("thebasics:notes-error-duplicate-id", note.Id);
                return false;
            }

            var normalizedText = NormalizeText(submittedNote.Text);
            var normalizedTitle = NormalizeTitle(submittedNote.Title);
            if (!string.Equals(note.Text ?? string.Empty, normalizedText, StringComparison.Ordinal) ||
                !string.Equals(note.Title ?? string.Empty, normalizedTitle, StringComparison.Ordinal))
            {
                note.Text = normalizedText;
                note.Title = normalizedTitle;
                note.UpdatedUtc = NowUtc();
                if (matched != null)
                {
                    summary.Updated++;
                }
            }

            note.Kind = replacement.Kind;
            note.TargetPlayerUid = replacement.Target.Uid;
            note.TargetPlayerName = replacement.Target.Name;
            note.TargetCharacterId = replacement.Target.CharacterId;
            note.TargetCharacterName = replacement.Target.CharacterName;
            replacements.Add(note);
            if (matched == null)
            {
                summary.Added++;
            }
        }

        summary.Deleted += existing.Count(note => !seenIds.Contains(note.Id));
        replacement.List.RemoveAll(note => BelongsToScopeTarget(note, replacement.Kind, actor.PlayerUID, replacement.Target.Uid));
        replacement.List.AddRange(replacements);
        SortNotes(replacement.List);
        return true;
    }

    private bool SetLedger(PlayerNotesData data, IServerPlayer actor, PlayerNoteTarget target, string text, MutationSummary summary, out string error)
    {
        error = null;
        text = NormalizeText(text, trim: false);
        if (text.Length > Config.MaxFreeformNoteLength)
        {
            error = Lang.Get("thebasics:notes-error-ledger-too-long", Config.MaxFreeformNoteLength);
            return false;
        }

        var ledger = data.AdminLedgers.FirstOrDefault(candidate => string.Equals(candidate.TargetPlayerUid, target.Uid, StringComparison.OrdinalIgnoreCase));
        if (ledger == null)
        {
            ledger = new AdminNoteLedgerMessage
            {
                TargetPlayerUid = target.Uid
            };
            data.AdminLedgers.Add(ledger);
        }

        if (string.Equals(ledger.Text ?? string.Empty, text, StringComparison.Ordinal))
        {
            return true;
        }

        ledger.TargetPlayerName = target.Name;
        ledger.TargetCharacterId = target.CharacterId;
        ledger.TargetCharacterName = target.CharacterName;
        ledger.Text = text;
        ledger.UpdatedUtc = NowUtc();
        ledger.UpdatedByPlayerUid = actor?.PlayerUID ?? string.Empty;
        ledger.UpdatedByName = actor?.PlayerName ?? "console";
        summary.FreeformChanged = true;
        return true;
    }

    private bool SetPersonalLedger(PlayerNotesData data, IServerPlayer actor, PlayerNoteTarget target, string text, MutationSummary summary, out string error)
    {
        error = null;
        text = NormalizeText(text, trim: false);
        if (text.Length > Config.MaxFreeformNoteLength)
        {
            error = Lang.Get("thebasics:notes-error-ledger-too-long", Config.MaxFreeformNoteLength);
            return false;
        }

        var ledger = data.PersonalLedgers.FirstOrDefault(candidate => BelongsToPersonalLedger(candidate, actor.PlayerUID, target.Uid));
        if (ledger == null)
        {
            ledger = new PersonalNoteLedgerMessage
            {
                AuthorPlayerUid = actor.PlayerUID,
                TargetPlayerUid = target.Uid
            };
            data.PersonalLedgers.Add(ledger);
        }

        if (string.Equals(ledger.Text ?? string.Empty, text, StringComparison.Ordinal))
        {
            return true;
        }

        ledger.TargetPlayerName = target.Name;
        ledger.TargetCharacterId = target.CharacterId;
        ledger.TargetCharacterName = target.CharacterName;
        ledger.Text = text;
        ledger.UpdatedUtc = NowUtc();
        summary.FreeformChanged = true;
        return true;
    }

    private bool CanAddNote(PlayerNotesData data, string kind, string actorUid, string targetUid, out string error)
    {
        error = null;
        if (kind == PlayerNotesConstants.KindAdmin)
        {
            if (CountAdminNotesForTarget(data, targetUid) >= Config.MaxAdminNotesPerTarget)
            {
                error = Lang.Get("thebasics:notes-error-too-many", Config.MaxAdminNotesPerTarget);
                return false;
            }

            return true;
        }

        if (data.PersonalNotes.Count(note => string.Equals(note.AuthorPlayerUid, actorUid, StringComparison.OrdinalIgnoreCase)) >= Config.MaxPlayerNotesPerAuthor)
        {
            error = Lang.Get("thebasics:notes-error-too-many", Config.MaxPlayerNotesPerAuthor);
            return false;
        }

        return true;
    }

    private bool ValidateNoteText(string text, out string error)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            error = Lang.Get("thebasics:notes-error-empty");
            return false;
        }

        if (text.Length > Config.MaxNoteLength)
        {
            error = Lang.Get("thebasics:notes-error-too-long", Config.MaxNoteLength);
            return false;
        }

        error = null;
        return true;
    }

    private bool ValidateNoteEntry(string title, string text, out string error)
    {
        title = NormalizeTitle(title);
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text))
        {
            error = Lang.Get("thebasics:notes-error-entry-empty");
            return false;
        }

        if (title.Length > MaxNoteTitleLength)
        {
            error = Lang.Get("thebasics:notes-error-title-too-long", MaxNoteTitleLength);
            return false;
        }

        if (text.Length > Config.MaxNoteLength)
        {
            error = Lang.Get("thebasics:notes-error-too-long", Config.MaxNoteLength);
            return false;
        }

        error = null;
        return true;
    }

    private PlayerNoteEntryMessage CreateNote(string kind, IServerPlayer actor, PlayerNoteTarget target, string text)
    {
        var now = NowUtc();
        return new PlayerNoteEntryMessage
        {
            Id = GenerateNoteId(),
            Kind = kind,
            AuthorPlayerUid = actor?.PlayerUID ?? string.Empty,
            AuthorName = actor?.PlayerName ?? "console",
            TargetPlayerUid = target.Uid,
            TargetPlayerName = target.Name,
            TargetCharacterId = target.CharacterId,
            TargetCharacterName = target.CharacterName,
            CreatedUtc = now,
            UpdatedUtc = now,
            Title = string.Empty,
            Text = NormalizeText(text)
        };
    }

    private List<PlayerNoteEntryMessage> GetAdminNotes(string targetUid)
    {
        return GetAdminNotes(targetUid, _store.Load());
    }

    private static List<PlayerNoteEntryMessage> GetAdminNotes(string targetUid, PlayerNotesData data)
    {
        return data.AdminNotes
            .Where(note => note.Kind == PlayerNotesConstants.KindAdmin && string.Equals(note.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(note => note.CreatedUtc, StringComparer.Ordinal)
            .ToList();
    }

    private List<PlayerNoteEntryMessage> GetPersonalNotes(string authorUid, string targetUid)
    {
        return GetPersonalNotes(authorUid, targetUid, _store.Load());
    }

    private static List<PlayerNoteEntryMessage> GetPersonalNotes(string authorUid, string targetUid, PlayerNotesData data)
    {
        return data.PersonalNotes
            .Where(note => BelongsToPersonalTarget(note, authorUid, targetUid))
            .OrderByDescending(note => note.CreatedUtc, StringComparer.Ordinal)
            .ToList();
    }

    private AdminNoteLedgerMessage GetLedger(PlayerNoteTarget target, PlayerNotesData data)
    {
        var ledger = data.AdminLedgers.FirstOrDefault(candidate => string.Equals(candidate.TargetPlayerUid, target.Uid, StringComparison.OrdinalIgnoreCase));
        return ledger ?? new AdminNoteLedgerMessage
        {
            TargetPlayerUid = target.Uid,
            TargetPlayerName = target.Name,
            TargetCharacterId = target.CharacterId,
            TargetCharacterName = target.CharacterName
        };
    }

    private PersonalNoteLedgerMessage GetPersonalLedger(string authorUid, PlayerNoteTarget target, PlayerNotesData data)
    {
        var ledger = data.PersonalLedgers.FirstOrDefault(candidate => BelongsToPersonalLedger(candidate, authorUid, target.Uid));
        return ledger ?? new PersonalNoteLedgerMessage
        {
            AuthorPlayerUid = authorUid,
            TargetPlayerUid = target.Uid,
            TargetPlayerName = target.Name,
            TargetCharacterId = target.CharacterId,
            TargetCharacterName = target.CharacterName
        };
    }

    private bool TryResolveTarget(string targetUid, string query, bool allowOffline, out PlayerNoteTarget target, out string error)
    {
        target = null;
        error = null;
        targetUid = (targetUid ?? string.Empty).Trim();
        query = (query ?? string.Empty).Trim();

        foreach (var onlinePlayer in API.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            if ((!string.IsNullOrWhiteSpace(targetUid) && onlinePlayer.PlayerUID.Equals(targetUid, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(query) && (onlinePlayer.PlayerUID.Equals(query, StringComparison.OrdinalIgnoreCase) || onlinePlayer.PlayerName.Equals(query, StringComparison.OrdinalIgnoreCase))))
            {
                target = PlayerNoteTarget.FromPlayer(onlinePlayer, GetActiveCharacter(onlinePlayer));
                return true;
            }
        }

        if (allowOffline)
        {
            foreach (var pair in API.PlayerData.PlayerDataByUid)
            {
                var playerData = pair.Value;
                var name = playerData?.LastKnownPlayername ?? string.Empty;
                if ((!string.IsNullOrWhiteSpace(targetUid) && pair.Key.Equals(targetUid, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(query) && (pair.Key.Equals(query, StringComparison.OrdinalIgnoreCase) || name.Equals(query, StringComparison.OrdinalIgnoreCase))))
                {
                    target = new PlayerNoteTarget(pair.Key, string.IsNullOrWhiteSpace(name) ? pair.Key : name, false, string.Empty, string.Empty);
                    return true;
                }
            }
        }

        error = Lang.Get("thebasics:notes-error-target-not-found", string.IsNullOrWhiteSpace(query) ? targetUid : query);
        return false;
    }

    private PlayerNoteTarget ResolvePersonalTarget(IServerPlayer actor, string targetUid, string query, out string error)
    {
        if (string.IsNullOrWhiteSpace(targetUid) && string.IsNullOrWhiteSpace(query))
        {
            error = null;
            return PlayerNoteTarget.FromPlayer(actor, GetActiveCharacter(actor));
        }

        return TryResolveTarget(targetUid, query, allowOffline: true, out var target, out error) ? target : null;
    }

    private static RpCharacterRecord GetActiveCharacter(IServerPlayer player)
    {
        if (player == null)
        {
            return null;
        }

        try
        {
            var activeId = player.GetActiveRpCharacterId();
            var registry = IServerPlayerExtensions.GetModData(player, RpCharacterService.CharacterSlotsKey, new RpCharacterRegistry()) ?? new RpCharacterRegistry();
            return registry.Characters?.FirstOrDefault(character => character != null && character.CharacterId.Equals(activeId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private void PushNotesView(IServerPlayer player, TheBasicsNotesViewMessage view)
    {
        API.ModLoader.GetModSystem<RPProximityChatSystem>()?.PushNotesView(player, view);
    }

    private bool CanUseAdminNotes(IServerPlayer player, bool allowConsole = true)
    {
        return Config.EnableAdminNotes &&
            (Config.EnableStructuredAdminNotes || Config.EnableAdminNoteLedger) &&
            HasAdminNotesPrivilege(player, allowConsole);
    }

    private bool CanUseStructuredAdminNotes(IServerPlayer player, bool allowConsole = true)
    {
        return Config.EnableAdminNotes && Config.EnableStructuredAdminNotes && HasAdminNotesPrivilege(player, allowConsole);
    }

    private bool HasAdminNotesPrivilege(IServerPlayer player, bool allowConsole)
    {
        return player == null ? allowConsole : player.HasPrivilege(Config.AdminNotesPermission);
    }

    private bool CanUsePersonalNotes(IServerPlayer player)
    {
        return Config.EnablePlayerNotes && player?.HasPrivilege(Config.PlayerNotesPermission) == true;
    }

    private static TheBasicsNotesViewMessage ErrorView(string scope, string message)
    {
        return new TheBasicsNotesViewMessage
        {
            Success = false,
            Scope = scope,
            Message = message ?? string.Empty
        };
    }

    private static bool BelongsToScopeTarget(PlayerNoteEntryMessage note, string kind, string actorUid, string targetUid)
    {
        if (note == null || !string.Equals(note.Kind, kind, StringComparison.OrdinalIgnoreCase) || !string.Equals(note.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return kind == PlayerNotesConstants.KindAdmin || string.Equals(note.AuthorPlayerUid, actorUid, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStructuredAdminAction(string action)
    {
        return action is "list" or "add" or "view" or "edit" or "delete";
    }

    private static bool BelongsToPersonalTarget(PlayerNoteEntryMessage note, string authorUid, string targetUid)
    {
        return note != null &&
               note.Kind == PlayerNotesConstants.KindPersonal &&
               string.Equals(note.AuthorPlayerUid, authorUid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(note.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase);
    }

    private static bool BelongsToPersonalLedger(PersonalNoteLedgerMessage ledger, string authorUid, string targetUid)
    {
        return ledger != null &&
               string.Equals(ledger.AuthorPlayerUid, authorUid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ledger.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountAdminNotesForTarget(PlayerNotesData data, string targetUid)
    {
        return data.AdminNotes.Count(note => note.Kind == PlayerNotesConstants.KindAdmin && string.Equals(note.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountPersonalNotesOutsideTarget(PlayerNotesData data, string authorUid, string targetUid)
    {
        return data.PersonalNotes.Count(note =>
            note.Kind == PlayerNotesConstants.KindPersonal &&
            string.Equals(note.AuthorPlayerUid, authorUid, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(note.TargetPlayerUid, targetUid, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IdEquals(PlayerNoteEntryMessage note, string id)
    {
        return note != null && !string.IsNullOrWhiteSpace(id) && string.Equals(note.Id, id, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveNote(IReadOnlyList<PlayerNoteEntryMessage> notes, string idOrPrefix, out PlayerNoteEntryMessage note, out string error)
    {
        note = ResolveNote(notes, idOrPrefix);
        if (note != null)
        {
            error = null;
            return true;
        }

        error = Lang.Get("thebasics:notes-error-note-not-found", idOrPrefix);
        return false;
    }

    private static PlayerNoteEntryMessage ResolveNote(IReadOnlyList<PlayerNoteEntryMessage> notes, string idOrPrefix)
    {
        idOrPrefix = (idOrPrefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(idOrPrefix))
        {
            return null;
        }

        var exact = notes.FirstOrDefault(note => IdEquals(note, idOrPrefix));
        if (exact != null)
        {
            return exact;
        }

        var matches = notes.Where(note => (note.Id ?? string.Empty).StartsWith(idOrPrefix, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static void SortNotes(List<PlayerNoteEntryMessage> notes)
    {
        notes.Sort((left, right) => string.CompareOrdinal(left.CreatedUtc, right.CreatedUtc));
    }

    private static PlayerNoteEntryMessage CloneNote(PlayerNoteEntryMessage note)
    {
        return note == null ? new PlayerNoteEntryMessage() : new PlayerNoteEntryMessage
        {
            Id = note.Id ?? string.Empty,
            Kind = note.Kind ?? string.Empty,
            AuthorPlayerUid = note.AuthorPlayerUid ?? string.Empty,
            AuthorName = note.AuthorName ?? string.Empty,
            TargetPlayerUid = note.TargetPlayerUid ?? string.Empty,
            TargetPlayerName = note.TargetPlayerName ?? string.Empty,
            TargetCharacterId = note.TargetCharacterId ?? string.Empty,
            TargetCharacterName = note.TargetCharacterName ?? string.Empty,
            CreatedUtc = note.CreatedUtc ?? string.Empty,
            UpdatedUtc = note.UpdatedUtc ?? string.Empty,
            Title = note.Title ?? string.Empty,
            Text = note.Text ?? string.Empty
        };
    }

    private static AdminNoteLedgerMessage CloneLedger(AdminNoteLedgerMessage ledger)
    {
        return ledger == null ? new AdminNoteLedgerMessage() : new AdminNoteLedgerMessage
        {
            TargetPlayerUid = ledger.TargetPlayerUid ?? string.Empty,
            TargetPlayerName = ledger.TargetPlayerName ?? string.Empty,
            TargetCharacterId = ledger.TargetCharacterId ?? string.Empty,
            TargetCharacterName = ledger.TargetCharacterName ?? string.Empty,
            UpdatedUtc = ledger.UpdatedUtc ?? string.Empty,
            UpdatedByPlayerUid = ledger.UpdatedByPlayerUid ?? string.Empty,
            UpdatedByName = ledger.UpdatedByName ?? string.Empty,
            Text = ledger.Text ?? string.Empty
        };
    }

    private static PersonalNoteLedgerMessage ClonePersonalLedger(PersonalNoteLedgerMessage ledger)
    {
        return ledger == null ? new PersonalNoteLedgerMessage() : new PersonalNoteLedgerMessage
        {
            AuthorPlayerUid = ledger.AuthorPlayerUid ?? string.Empty,
            TargetPlayerUid = ledger.TargetPlayerUid ?? string.Empty,
            TargetPlayerName = ledger.TargetPlayerName ?? string.Empty,
            TargetCharacterId = ledger.TargetCharacterId ?? string.Empty,
            TargetCharacterName = ledger.TargetCharacterName ?? string.Empty,
            UpdatedUtc = ledger.UpdatedUtc ?? string.Empty,
            Text = ledger.Text ?? string.Empty
        };
    }

    private static string RenderNoteList(IReadOnlyList<PlayerNoteEntryMessage> notes, string emptyMessage)
    {
        if (notes.Count == 0)
        {
            return emptyMessage;
        }

        var builder = new StringBuilder();
        foreach (var note in notes)
        {
            builder.AppendLine(FormatNoteSummary(note));
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderNote(PlayerNoteEntryMessage note)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatNoteSummary(note));
        if (!string.IsNullOrWhiteSpace(note.Title))
        {
            builder.AppendLine(VtmlUtils.EscapeVtml(note.Title));
        }

        if (!string.IsNullOrWhiteSpace(note.Text))
        {
            builder.Append(VtmlUtils.EscapeVtml(note.Text));
        }

        return builder.ToString();
    }

    private static string FormatNoteSummary(PlayerNoteEntryMessage note)
    {
        var created = ShortDate(note.CreatedUtc);
        var text = string.IsNullOrWhiteSpace(note.Title)
            ? (note.Text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim()
            : note.Title.Trim();
        if (text.Length > 80)
        {
            text = text.Substring(0, 80) + "...";
        }

        return $"#{VtmlUtils.EscapeVtml(note.Id)} [{created}] {VtmlUtils.EscapeVtml(note.AuthorName)}: {VtmlUtils.EscapeVtml(text)}";
    }

    private static string ShortDate(string utc)
    {
        return DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "unknown";
    }

    private static string GetRawArgument(TextCommandCallingArgs args)
    {
        return args.Parsers.Count > 0 ? args.Parsers[0].GetValue()?.ToString() ?? string.Empty : string.Empty;
    }

    private static string PopToken(ref string input)
    {
        input = (input ?? string.Empty).TrimStart();
        if (input.Length == 0)
        {
            return string.Empty;
        }

        if (input[0] == '"')
        {
            for (var i = 1; i < input.Length; i++)
            {
                if (input[i] == '"')
                {
                    var token = input.Substring(1, i - 1);
                    input = input.Substring(i + 1).TrimStart();
                    return token;
                }
            }

            var unterminated = input.Substring(1);
            input = string.Empty;
            return unterminated;
        }

        var split = input.IndexOf(' ');
        if (split < 0)
        {
            var token = input;
            input = string.Empty;
            return token;
        }

        var result = input.Substring(0, split);
        input = input.Substring(split + 1).TrimStart();
        return result;
    }

    private static string NormalizeScope(string scope)
    {
        return string.Equals(scope, PlayerNotesConstants.ScopeAdmin, StringComparison.OrdinalIgnoreCase)
            ? PlayerNotesConstants.ScopeAdmin
            : PlayerNotesConstants.ScopePersonal;
    }

    private static string NormalizeText(string text, bool trim = true)
    {
        text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        return trim ? text.Trim() : text;
    }

    private static string NormalizeTitle(string title)
    {
        return (title ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string NowUtc()
    {
        return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string GenerateNoteId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
    }

    private static string ActorLabel(IServerPlayer actor)
    {
        return actor == null ? "console" : $"{actor.PlayerName} ({actor.PlayerUID})";
    }

    private enum MutationKind
    {
        Add,
        Edit,
        Delete
    }

    private sealed class MutationSummary
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
        public bool FreeformChanged { get; set; }
        public bool HasChanges => Added > 0 || Updated > 0 || Deleted > 0 || FreeformChanged;
    }

    private sealed class NoteReplacement
    {
        public NoteReplacement(List<PlayerNoteEntryMessage> list, IEnumerable<PlayerNoteEntryMessage> submitted, string kind, PlayerNoteTarget target, int maxCount, int countOutsideTarget)
        {
            List = list;
            Submitted = submitted;
            Kind = kind;
            Target = target;
            MaxCount = maxCount;
            CountOutsideTarget = countOutsideTarget;
        }

        public List<PlayerNoteEntryMessage> List { get; }
        public IEnumerable<PlayerNoteEntryMessage> Submitted { get; }
        public string Kind { get; }
        public PlayerNoteTarget Target { get; }
        public int MaxCount { get; }
        public int CountOutsideTarget { get; }
    }

    private sealed class PlayerNoteTarget
    {
        public PlayerNoteTarget(string uid, string name, bool online, string characterId, string characterName)
        {
            Uid = uid ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? Uid : name;
            Online = online;
            CharacterId = characterId ?? string.Empty;
            CharacterName = characterName ?? string.Empty;
        }

        public string Uid { get; }
        public string Name { get; }
        public bool Online { get; }
        public string CharacterId { get; }
        public string CharacterName { get; }

        public static PlayerNoteTarget FromPlayer(IServerPlayer player, RpCharacterRecord character)
        {
            return new PlayerNoteTarget(player.PlayerUID, player.PlayerName, true, character?.CharacterId, character?.DisplayName);
        }
    }
}
