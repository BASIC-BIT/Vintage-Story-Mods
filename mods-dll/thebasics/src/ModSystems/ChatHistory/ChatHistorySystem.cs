#pragma warning disable S1200, S1541 // Chat capture intentionally bridges config, proximity metadata, and server APIs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.ChatHistory.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ChatHistory;

public class ChatHistorySystem : BaseBasicModSystem
{
    private readonly object _pendingLock = new();
    private readonly List<ChatHistoryEntry> _pending = new();
    private ChatHistoryStore _store;
    private bool _disposed;

    protected override void BasicStartServerSide()
    {
        _store = new ChatHistoryStore(API);
        TryRegisterConfiguredPrivilege(Config.ChatHistoryPermission, "Allows access to The BASICs chat history search.");
        TryRegisterConfiguredPrivilege(Config.ChatHistoryManagePermission, "Allows export and purge management for The BASICs chat history.");
        RegisterCommands();
        API.Event.PlayerChat += Event_PlayerChat;
        API.Event.GameWorldSave += Event_GameWorldSave;
        API.Event.Timer(FlushQueued, Math.Max(0.1, Config.ChatHistoryFlushIntervalMilliseconds / 1000.0));
    }

    public override void Dispose()
    {
        if (API != null)
        {
            API.Event.PlayerChat -= Event_PlayerChat;
            API.Event.GameWorldSave -= Event_GameWorldSave;
        }

        FlushQueued();
        _disposed = true;
        base.Dispose();
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("chatlog")
            .WithAlias("chathistory")
            .WithDescription(Lang.Get("thebasics:chat-history-cmd-desc"))
            .RequiresPrivilege(Config.ChatHistoryPermission)
            .WithArgs(new StringArgParser("action", false))
            .HandleWith(HandleChatLogCommand);
    }

    protected override void OnConfigReloaded(IReadOnlySet<string> changedKeys)
    {
        if (changedKeys.Contains(nameof(Config.ChatHistoryPermission)))
        {
            API.ChatCommands.Get("chatlog")?.RequiresPrivilege(Config.ChatHistoryPermission);
            TryRegisterConfiguredPrivilege(Config.ChatHistoryPermission, "Allows access to The BASICs chat history search.");
        }

        if (changedKeys.Contains(nameof(Config.ChatHistoryManagePermission)))
        {
            TryRegisterConfiguredPrivilege(Config.ChatHistoryManagePermission, "Allows export and purge management for The BASICs chat history.");
        }
    }

    public void RecordBasicChat(MessageContext context, string formattedMessage)
    {
        if (!Config.EnableChatHistory || context?.SendingPlayer == null)
        {
            return;
        }

        Enqueue(BuildBasicEntry(context, formattedMessage));
    }

    public ChatHistoryQueryResult Query(ChatHistoryQuery query)
    {
        FlushQueued();
        return _store.Query(query, Config.ChatHistorySearchMaxResults, Config.ChatHistorySearchMaxResults);
    }

    public TheBasicsChatHistoryResultMessage HandleGuiRequest(IServerPlayer player, TheBasicsChatHistoryQueryRequest request)
    {
        request ??= new TheBasicsChatHistoryQueryRequest { Limit = 10 };
        if (!Config.EnableChatHistory)
        {
            return BuildGuiError(player, request, Lang.Get("thebasics:chat-history-disabled"));
        }

        if (!HasReadPrivilege(player))
        {
            return BuildGuiError(player, request, Lang.Get("thebasics:chat-history-error-permission"));
        }

        return request.Export ? HandleGuiExport(player, request) : BuildGuiResult(player, request);
    }

    public ChatHistoryRetentionResult ApplyRetention()
    {
        FlushQueued();
        return _store.ApplyRetention(Config.ChatHistoryRetentionDays, Config.ChatHistoryMaxEntries, DateTime.UtcNow);
    }

    private TextCommandResult HandleChatLogCommand(TextCommandCallingArgs args)
    {
        if (!Config.EnableChatHistory)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-disabled"));
        }

        var raw = GetRawArgument(args);
        var action = PopToken(ref raw).ToLowerInvariant();
        return action switch
        {
            "" => HandleDefaultChatLogAction(args, raw),
            "gui" or "open" => HandleOpenGui(args.Caller.Player as IServerPlayer),
            "recent" => HandleRecent(raw),
            "help" or "?" => TextCommandResult.Success(Lang.Get("thebasics:chat-history-help")),
            "search" => HandleSearch(raw),
            "player" => HandlePlayerSearch(raw),
            "view" => HandleView(raw),
            "purge" => HandlePurge(args.Caller.Player as IServerPlayer, raw),
            "export" => HandleExport(args.Caller.Player as IServerPlayer, raw),
            _ => TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-unknown-action", action))
        };
    }

    private TextCommandResult HandleDefaultChatLogAction(TextCommandCallingArgs args, string raw)
    {
        return args.Caller.Player is IServerPlayer player ? HandleOpenGui(player) : HandleRecent(raw);
    }

    private TextCommandResult HandleOpenGui(IServerPlayer player)
    {
        if (player == null)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-gui-player-only"));
        }

        var proximityChat = API.ModLoader.GetModSystem<RPProximityChatSystem>();
        if (proximityChat == null)
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-gui-unavailable"));
        }

        proximityChat.PushChatHistoryResult(player, HandleGuiRequest(player, new TheBasicsChatHistoryQueryRequest { Limit = 10 }));
        return TextCommandResult.Success(Lang.Get("thebasics:chat-history-gui-opened"));
    }

    private TextCommandResult HandleRecent(string raw)
    {
        var count = ParseOptionalCount(PopToken(ref raw), defaultValue: 20);
        return TextCommandResult.Success(RenderResults(Query(new ChatHistoryQuery { Limit = count })));
    }

    private TextCommandResult HandleSearch(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-search-usage"));
        }

        return TextCommandResult.Success(RenderResults(Query(new ChatHistoryQuery { SearchText = raw.Trim() })));
    }

    private TextCommandResult HandlePlayerSearch(string raw)
    {
        var player = PopToken(ref raw);
        if (string.IsNullOrWhiteSpace(player))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-player-usage"));
        }

        var count = ParseOptionalCount(PopToken(ref raw), defaultValue: Config.ChatHistorySearchMaxResults);
        return TextCommandResult.Success(RenderResults(Query(new ChatHistoryQuery { Player = player, Limit = count })));
    }

    private TextCommandResult HandleView(string raw)
    {
        var id = PopToken(ref raw);
        if (string.IsNullOrWhiteSpace(id))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-view-usage"));
        }

        FlushQueued();
        var matches = _store.LoadAll()
            .Where(entry => (entry.Id ?? string.Empty).StartsWith(id, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count switch
        {
            0 => TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-not-found", id)),
            > 1 => TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-ambiguous-id", id)),
            _ => TextCommandResult.Success(RenderDetails(matches[0]))
        };
    }

    private TextCommandResult HandlePurge(IServerPlayer actor, string raw)
    {
        if (!HasManagePrivilege(actor))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-manage-permission"));
        }

        var mode = PopToken(ref raw).ToLowerInvariant();
        return mode switch
        {
            "retention" => PurgeRetention(actor, PopToken(ref raw)),
            "before" => PurgeBefore(actor, PopToken(ref raw), PopToken(ref raw)),
            "all" => PurgeAll(actor, PopToken(ref raw)),
            _ => TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-purge-usage"))
        };
    }

    private TextCommandResult HandleExport(IServerPlayer actor, string raw)
    {
        if (!HasManagePrivilege(actor))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-manage-permission"));
        }

        var query = BuildExportQuery(raw);
        FlushQueued();
        var result = _store.Export(query, BuildExportPath());
        API.Logger.Audit($"Admin {ActorLabel(actor)} exported {result.ExportedCount} chat history entries to {result.Path}.");
        return TextCommandResult.Success(Lang.Get("thebasics:chat-history-export-success", result.ExportedCount, result.Path));
    }

    private TextCommandResult PurgeRetention(IServerPlayer actor, string confirm)
    {
        if (!IsConfirm(confirm))
        {
            return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-confirm", "retention confirm"));
        }

        var result = ApplyRetention();
        API.Logger.Audit($"Admin {ActorLabel(actor)} purged {result.RemovedCount} chat history entries using retention settings.");
        return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-success", result.RemovedCount));
    }

    private TextCommandResult PurgeBefore(IServerPlayer actor, string date, string confirm)
    {
        if (!TryParseUtc(date, out var cutoff))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:chat-history-error-invalid-date", date));
        }

        if (!IsConfirm(confirm))
        {
            return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-confirm", $"before {date} confirm"));
        }

        FlushQueued();
        var removed = _store.PurgeBefore(cutoff);
        API.Logger.Audit($"Admin {ActorLabel(actor)} purged {removed} chat history entries before {cutoff:O}.");
        return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-success", removed));
    }

    private TextCommandResult PurgeAll(IServerPlayer actor, string confirm)
    {
        if (!IsConfirm(confirm))
        {
            return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-confirm", "all confirm"));
        }

        FlushQueued();
        var removed = _store.PurgeAll();
        API.Logger.Audit($"Admin {ActorLabel(actor)} purged all chat history entries; removed {removed} entries.");
        return TextCommandResult.Success(Lang.Get("thebasics:chat-history-purge-success", removed));
    }

    private ChatHistoryQuery BuildExportQuery(string raw)
    {
        var action = PopToken(ref raw).ToLowerInvariant();
        if (action == "search")
        {
            return new ChatHistoryQuery { SearchText = raw.Trim() };
        }

        if (action == "player")
        {
            return new ChatHistoryQuery { Player = PopToken(ref raw) };
        }

        if (action == "recent")
        {
            return new ChatHistoryQuery { Limit = ParseOptionalCount(PopToken(ref raw), defaultValue: Config.ChatHistorySearchMaxResults) };
        }

        return new ChatHistoryQuery();
    }

    private TheBasicsChatHistoryResultMessage HandleGuiExport(IServerPlayer player, TheBasicsChatHistoryQueryRequest request)
    {
        if (!HasManagePrivilege(player))
        {
            return BuildGuiError(player, request, Lang.Get("thebasics:chat-history-error-manage-permission"));
        }

        var exportQuery = BuildQuery(request);
        exportQuery.Offset = 0;
        exportQuery.Limit = 0;

        FlushQueued();
        var export = _store.Export(exportQuery, BuildExportPath());
        API.Logger.Audit($"Admin {ActorLabel(player)} exported {export.ExportedCount} chat history entries to {export.Path} from the GUI.");

        var response = BuildGuiResult(player, request);
        response.Message = Lang.Get("thebasics:chat-history-export-success", export.ExportedCount, export.Path);
        response.ExportedCount = export.ExportedCount;
        response.ExportPath = export.Path;
        return response;
    }

    private TheBasicsChatHistoryResultMessage BuildGuiResult(IServerPlayer player, TheBasicsChatHistoryQueryRequest request)
    {
        var query = BuildQuery(request);
        if (query.Limit <= 0)
        {
            query.Limit = 10;
        }

        var result = Query(query);
        var responseQuery = CloneRequest(request);
        responseQuery.Export = false;
        responseQuery.Offset = result.Offset;
        responseQuery.Limit = result.Limit;

        return new TheBasicsChatHistoryResultMessage
        {
            Success = true,
            Query = responseQuery,
            Entries = result.Entries.Select(ToGuiEntry).ToList(),
            TotalMatches = result.TotalMatches,
            Offset = result.Offset,
            Limit = result.Limit,
            CanManage = HasManagePrivilege(player)
        };
    }

    private TheBasicsChatHistoryResultMessage BuildGuiError(IServerPlayer player, TheBasicsChatHistoryQueryRequest request, string message)
    {
        return new TheBasicsChatHistoryResultMessage
        {
            Success = false,
            Message = message ?? string.Empty,
            Query = CloneRequest(request),
            CanManage = HasManagePrivilege(player)
        };
    }

    private static ChatHistoryQuery BuildQuery(TheBasicsChatHistoryQueryRequest request)
    {
        request ??= new TheBasicsChatHistoryQueryRequest();
        return new ChatHistoryQuery
        {
            SearchText = request.SearchText ?? string.Empty,
            Player = request.Player ?? string.Empty,
            ChannelId = request.HasChannelId ? request.ChannelId : null,
            ChatKind = request.ChatKind ?? string.Empty,
            ProximityMode = request.ProximityMode ?? string.Empty,
            Language = request.Language ?? string.Empty,
            FromUtc = request.FromUtc ?? string.Empty,
            ToUtc = request.ToUtc ?? string.Empty,
            Offset = Math.Max(0, request.Offset),
            Limit = Math.Max(0, request.Limit)
        };
    }

    private static TheBasicsChatHistoryQueryRequest CloneRequest(TheBasicsChatHistoryQueryRequest request)
    {
        request ??= new TheBasicsChatHistoryQueryRequest();
        return new TheBasicsChatHistoryQueryRequest
        {
            SearchText = request.SearchText ?? string.Empty,
            Player = request.Player ?? string.Empty,
            ChannelId = request.ChannelId,
            HasChannelId = request.HasChannelId,
            ChatKind = request.ChatKind ?? string.Empty,
            ProximityMode = request.ProximityMode ?? string.Empty,
            Language = request.Language ?? string.Empty,
            FromUtc = request.FromUtc ?? string.Empty,
            ToUtc = request.ToUtc ?? string.Empty,
            Offset = Math.Max(0, request.Offset),
            Limit = Math.Max(0, request.Limit),
            Export = request.Export
        };
    }

    private static TheBasicsChatHistoryEntryMessage ToGuiEntry(ChatHistoryEntry entry)
    {
        entry = ChatHistoryStore.Normalize(entry);
        return new TheBasicsChatHistoryEntryMessage
        {
            Id = entry.Id,
            TimestampUtc = entry.TimestampUtc,
            Source = entry.Source,
            ChatKind = entry.ChatKind,
            SenderPlayerUid = entry.SenderPlayerUid,
            SenderPlayerName = entry.SenderPlayerName,
            SenderNickname = entry.SenderNickname,
            ChannelId = entry.ChannelId,
            ChannelName = entry.ChannelName,
            ProximityMode = entry.ProximityMode,
            Language = entry.Language,
            MessageText = entry.MessageText,
            FormattedMessage = entry.FormattedMessage,
            RawEventMessage = entry.RawEventMessage,
            ClientData = entry.ClientData,
            IsFromCommand = entry.IsFromCommand,
            IsPlayerChat = entry.IsPlayerChat,
            RecipientCount = entry.RecipientCount,
            PendingRecipientCount = entry.PendingRecipientCount,
            SenderPosition = FormatPosition(entry.SenderX, entry.SenderY, entry.SenderZ),
            PlacedPosition = FormatPosition(entry.PlacedX, entry.PlacedY, entry.PlacedZ)
        };
    }

    private string BuildExportPath()
    {
        var savegameId = ChatHistoryStore.SanitizePathPart(API.WorldManager?.SaveGame?.SavegameIdentifier ?? "default");
        var dir = Path.Combine(API.GetOrCreateDataPath("ModData"), "thebasics", "chat-history-exports", savegameId);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);
        var filename = $"chat-history-{timestamp}-{Guid.NewGuid():N}.jsonl";
        return Path.Combine(dir, filename);
    }

    private static string RenderResults(ChatHistoryQueryResult result)
    {
        if (result.Entries.Count == 0)
        {
            return Lang.Get("thebasics:chat-history-no-results");
        }

        var builder = new StringBuilder();
        builder.AppendLine(Lang.Get("thebasics:chat-history-results-header", result.Entries.Count, result.TotalMatches));
        foreach (var entry in result.Entries)
        {
            builder.AppendLine(RenderSummary(entry));
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderSummary(ChatHistoryEntry entry)
    {
        var sender = string.IsNullOrWhiteSpace(entry.SenderPlayerName) ? "unknown" : entry.SenderPlayerName;
        var kind = string.IsNullOrWhiteSpace(entry.ChatKind) ? ChatHistoryConstants.KindUnknown : entry.ChatKind;
        return $"#{VtmlUtils.EscapeVtml(ShortId(entry.Id))} [{ShortDate(entry.TimestampUtc)}] [{VtmlUtils.EscapeVtml(kind)}] {VtmlUtils.EscapeVtml(sender)}: {VtmlUtils.EscapeVtml(Preview(entry.MessageText))}";
    }

    private static string RenderDetails(ChatHistoryEntry entry)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"#{VtmlUtils.EscapeVtml(entry.Id)}");
        builder.AppendLine($"Time: {VtmlUtils.EscapeVtml(entry.TimestampUtc)}");
        builder.AppendLine($"Source: {VtmlUtils.EscapeVtml(entry.Source)}");
        builder.AppendLine($"Kind: {VtmlUtils.EscapeVtml(entry.ChatKind)}");
        builder.AppendLine($"Sender: {VtmlUtils.EscapeVtml(entry.SenderPlayerName)} ({VtmlUtils.EscapeVtml(entry.SenderPlayerUid)})");
        if (!string.IsNullOrWhiteSpace(entry.SenderNickname)) builder.AppendLine($"Nickname: {VtmlUtils.EscapeVtml(entry.SenderNickname)}");
        builder.AppendLine($"Channel: {entry.ChannelId} {VtmlUtils.EscapeVtml(entry.ChannelName)}");
        if (!string.IsNullOrWhiteSpace(entry.ProximityMode)) builder.AppendLine($"Mode: {VtmlUtils.EscapeVtml(entry.ProximityMode)}");
        if (!string.IsNullOrWhiteSpace(entry.Language)) builder.AppendLine($"Language: {VtmlUtils.EscapeVtml(entry.Language)}");
        builder.AppendLine($"Recipients: {entry.RecipientCount} immediate, {entry.PendingRecipientCount} pending");
        if (entry.SenderX != null) builder.AppendLine($"Sender position: {FormatCoord(entry.SenderX)}, {FormatCoord(entry.SenderY)}, {FormatCoord(entry.SenderZ)}");
        if (entry.PlacedX != null) builder.AppendLine($"Placed position: {FormatCoord(entry.PlacedX)}, {FormatCoord(entry.PlacedY)}, {FormatCoord(entry.PlacedZ)}");
        builder.AppendLine("Message:");
        builder.Append(VtmlUtils.EscapeVtml(entry.MessageText));
        if (!string.IsNullOrWhiteSpace(entry.FormattedMessage))
        {
            builder.AppendLine();
            builder.AppendLine("Formatted:");
            builder.Append(VtmlUtils.EscapeVtml(entry.FormattedMessage));
        }

        return builder.ToString();
    }

    private bool HasManagePrivilege(IServerPlayer player)
    {
        return player == null || player.HasPrivilege(Config.ChatHistoryManagePermission);
    }

    private bool HasReadPrivilege(IServerPlayer player)
    {
        return player == null || player.HasPrivilege(Config.ChatHistoryPermission);
    }

    private static bool IsConfirm(string value)
    {
        return string.Equals(value, "confirm", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseOptionalCount(string value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static bool TryParseUtc(string value, out DateTime timestamp)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    private static string ShortId(string id)
    {
        return string.IsNullOrWhiteSpace(id) || id.Length <= 12 ? id ?? string.Empty : id[..12];
    }

    private static string ShortDate(string utc)
    {
        return TryParseUtc(utc, out var parsed)
            ? parsed.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "unknown";
    }

    private static string Preview(string value)
    {
        value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length > 120 ? value[..120] + "..." : value;
    }

    private static string FormatCoord(double? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
    }

    private static string FormatPosition(double? x, double? y, double? z)
    {
        return x == null ? string.Empty : $"{FormatCoord(x)}, {FormatCoord(y)}, {FormatCoord(z)}";
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

    private static string ActorLabel(IServerPlayer actor)
    {
        return actor == null ? "console" : $"{actor.PlayerName} ({actor.PlayerUID})";
    }

    private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data, BoolRef consumed)
    {
        if (!Config.EnableChatHistory || !Config.ChatHistoryCaptureNonBasicChat || byPlayer == null)
        {
            return;
        }

        if (consumed?.value == true || IsHandledByTheBasics(byPlayer, channelId))
        {
            return;
        }

        var text = ChatHelper.GetMessage(message);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Enqueue(BuildPlayerChatEntry(byPlayer, channelId, text, message, data));
    }

    private ChatHistoryEntry BuildBasicEntry(MessageContext context, string formattedMessage)
    {
        var sender = context.SendingPlayer;
        var entry = BuildBaseEntry(sender, context.GroupId);
        entry.Source = ChatHistoryConstants.SourceTheBasics;
        entry.ChatKind = DetermineBasicKind(context);
        entry.MessageText = context.Message ?? string.Empty;
        entry.FormattedMessage = formattedMessage ?? string.Empty;
        entry.IsFromCommand = context.HasFlag(MessageContext.IS_FROM_COMMAND);
        entry.IsPlayerChat = context.HasFlag(MessageContext.IS_PLAYER_CHAT);
        entry.RecipientCount = context.Recipients?.Count ?? 0;
        entry.PendingRecipientCount = GetPendingRecipientCount(context);

        if (context.TryGetMetadata(MessageContext.CHAT_MODE, out ProximityChatMode mode))
        {
            entry.ProximityMode = mode.ToString();
        }

        if (context.TryGetMetadata(MessageContext.LANGUAGE, out Language language))
        {
            entry.Language = language?.Name ?? string.Empty;
        }

        if (context.TryGetMetadata("clientData", out string clientData))
        {
            entry.ClientData = clientData ?? string.Empty;
        }

        if (context.TryGetMetadata(MessageContext.PLACED_POSITION, out Vec3d placedPosition))
        {
            entry.PlacedX = placedPosition.X;
            entry.PlacedY = placedPosition.Y;
            entry.PlacedZ = placedPosition.Z;
        }

        return entry;
    }

    private ChatHistoryEntry BuildPlayerChatEntry(IServerPlayer player, int channelId, string text, string rawMessage, string clientData)
    {
        var entry = BuildBaseEntry(player, channelId);
        entry.Source = ChatHistoryConstants.SourcePlayerChatEvent;
        entry.ChatKind = ChatHistoryConstants.KindPlayerChat;
        entry.MessageText = text;
        entry.RawEventMessage = rawMessage ?? string.Empty;
        entry.ClientData = clientData ?? string.Empty;
        entry.IsPlayerChat = true;
        return entry;
    }

    private ChatHistoryEntry BuildBaseEntry(IServerPlayer player, int channelId)
    {
        var entry = new ChatHistoryEntry
        {
            Id = GenerateId(),
            TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            SenderPlayerUid = player?.PlayerUID ?? string.Empty,
            SenderPlayerName = player?.PlayerName ?? string.Empty,
            SenderNickname = SafeGetNickname(player),
            ChannelId = channelId,
            ChannelName = ResolveChannelName(channelId)
        };

        if (player?.Entity != null)
        {
            entry.SenderX = player.Entity.Pos.X;
            entry.SenderY = player.Entity.Pos.Y;
            entry.SenderZ = player.Entity.Pos.Z;
        }

        return entry;
    }

    private void Enqueue(ChatHistoryEntry entry)
    {
        lock (_pendingLock)
        {
            _pending.Add(entry);
        }
    }

    private void Event_GameWorldSave()
    {
        FlushQueued();
        ApplyRetention();
    }

    private void FlushQueued()
    {
        if (_disposed)
        {
            return;
        }

        List<ChatHistoryEntry> batch;
        lock (_pendingLock)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            batch = new List<ChatHistoryEntry>(_pending);
            _pending.Clear();
        }

        try
        {
            _store.Append(batch);
        }
        catch (Exception ex)
        {
            API.Logger.Warning("[thebasics] Failed to flush chat history entries. " + ex.Message);
        }
    }

    private bool IsHandledByTheBasics(IServerPlayer player, int channelId)
    {
        var chatSystem = API.ModLoader.GetModSystem<RPProximityChatSystem>();
        return chatSystem != null && channelId == chatSystem.ProximityChatId && player.GetRpTextEnabled();
    }

    private string ResolveChannelName(int channelId)
    {
        if (channelId == GlobalConstants.GeneralChatGroup)
        {
            return "General";
        }

        return API.Groups.PlayerGroupsById.TryGetValue(channelId, out var group)
            ? group?.Name ?? string.Empty
            : string.Empty;
    }

    private static string DetermineBasicKind(MessageContext context)
    {
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL)) return ChatHistoryConstants.KindPlacedEnvironmental;
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL)) return ChatHistoryConstants.KindEnvironmental;
        if (context.HasFlag(MessageContext.IS_GLOBAL_OOC)) return ChatHistoryConstants.KindGlobalOoc;
        if (context.HasFlag(MessageContext.IS_OOC)) return ChatHistoryConstants.KindOoc;
        if (context.HasFlag(MessageContext.IS_EMOTE)) return ChatHistoryConstants.KindEmote;
        if (context.HasFlag(MessageContext.IS_SPEECH)) return ChatHistoryConstants.KindSpeech;
        return ChatHistoryConstants.KindUnknown;
    }

    private static int GetPendingRecipientCount(MessageContext context)
    {
        return context.TryGetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, out List<IServerPlayer> pendingRecipients)
            ? pendingRecipients.Count
            : 0;
    }

    private static string SafeGetNickname(IServerPlayer player)
    {
        try
        {
            return player?.GetNickname() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void TryRegisterConfiguredPrivilege(string privilege, string description)
    {
        if (string.IsNullOrWhiteSpace(privilege) || IsBuiltInPrivilege(privilege))
        {
            return;
        }

        try
        {
            API.Permissions.RegisterPrivilege(privilege, description);
        }
        catch (Exception ex)
        {
            API.Logger.Warning($"[thebasics] Could not register chat history privilege '{privilege}'. {ex.Message}");
        }
    }

    private static bool IsBuiltInPrivilege(string privilege)
    {
        return string.Equals(privilege, Privilege.chat, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.commandplayer, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.controlserver, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.root, StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
    }
}
