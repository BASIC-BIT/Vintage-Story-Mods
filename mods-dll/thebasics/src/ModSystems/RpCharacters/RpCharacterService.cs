using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.RpCharacters.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterService
{
    internal const string CharacterSlotsKey = "BASIC_CHARACTER_SLOTS";
    internal const string ActiveCharacterIdKey = "BASIC_ACTIVE_CHARACTER_ID";
    private const string CharacterSheetKey = "BASIC_CHARACTER_SHEET";
    private const string NicknameColorKey = "BASIC_NICKNAME_COLOR";
    private static readonly Dictionary<string, string> EnglishTemplates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rpchar-error-name-required"] = "Character name is required.",
        ["rpchar-error-max-active"] = "You already have the maximum {0} active RP characters.",
        ["rpchar-error-duplicate-name"] = "You already have an active RP character named '{0}'.",
        ["rpchar-error-valid-online-player"] = "A valid online player is required.",
        ["rpchar-error-switch-in-progress"] = "A character switch is already in progress for this account.",
        ["rpchar-error-no-match"] = "No active RP character matches '{0}'.",
        ["rpchar-error-archive-active"] = "Cannot archive the active RP character. Switch to another character first.",
        ["rpchar-success-created"] = "Created RP character '{0}' ({1}).",
        ["rpchar-success-already-active"] = "'{0}' is already active.",
        ["rpchar-success-switched"] = "Switched to RP character '{0}'.",
        ["rpchar-success-renamed"] = "Renamed RP character to '{0}'.",
        ["rpchar-success-archived"] = "Archived RP character '{0}'."
    };

    private readonly ModConfig _config;
    private readonly Func<string, object[], string> _localize;
    private readonly HashSet<string> _swapLocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public RpCharacterService(ModConfig config, Func<string, object[], string> localize = null)
    {
        _config = config ?? new ModConfig();
        _config.InitializeDefaultsIfNeeded();
        _localize = localize ?? EnglishText;
    }

    public RpCharacterRegistry EnsureRegistry(IServerPlayer player)
    {
        var registry = ReadRegistry(player);
        var activeId = GetActiveCharacterId(player);

        if (registry.Characters.Count == 0)
        {
            var defaultRecord = CreateRecord(GenerateCharacterId(registry, player.PlayerName), GetDefaultDisplayName(player), CaptureProjection(player));
            registry.Characters.Add(defaultRecord);
            activeId = defaultRecord.CharacterId;
            SaveRegistry(player, registry);
            SetActiveCharacterId(player, activeId);
            return registry;
        }

        var activeRecord = FindCharacter(registry, activeId, includeArchived: true);
        if (activeRecord == null || activeRecord.Archived)
        {
            activeRecord = registry.Characters.FirstOrDefault(character => !character.Archived) ?? registry.Characters[0];
            activeRecord.Archived = false;
            activeId = activeRecord.CharacterId;
            SetActiveCharacterId(player, activeId);
        }

        CaptureIntoRecord(player, activeRecord);
        SaveRegistry(player, registry);
        return registry;
    }

    public RpCharacterRegistry ReadRegistry(IServerPlayer player)
    {
        var registry = IServerPlayerExtensions.GetModData(player, CharacterSlotsKey, new RpCharacterRegistry()) ?? new RpCharacterRegistry();
        registry.Characters ??= new List<RpCharacterRecord>();
        registry.Version = registry.Version <= 0 ? 1 : registry.Version;

        foreach (var character in registry.Characters)
        {
            character.CharacterId ??= string.Empty;
            character.DisplayName ??= string.Empty;
            character.Projection ??= CreateDefaultProjection();
            NormalizeProjection(character.Projection);
        }

        return registry;
    }

    public string GetActiveCharacterId(IServerPlayer player)
    {
        return IServerPlayerExtensions.GetModData<string>(player, ActiveCharacterIdKey, string.Empty) ?? string.Empty;
    }

    public RpCharacterRecord GetActiveCharacter(IServerPlayer player)
    {
        var registry = ReadRegistry(player);
        return FindCharacter(registry, GetActiveCharacterId(player), includeArchived: true);
    }

    public RpCharacterOperationResult CreateCharacter(IServerPlayer player, string displayName, int maxCharacters)
    {
        var registry = EnsureRegistry(player);
        displayName = NormalizeDisplayName(displayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-name-required"));
        }

        if (maxCharacters > 0 && registry.Characters.Count(character => !character.Archived) >= maxCharacters)
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-max-active", maxCharacters));
        }

        if (registry.Characters.Any(character => !character.Archived && character.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-duplicate-name", Safe(displayName)));
        }

        var record = CreateRecord(GenerateCharacterId(registry, displayName), displayName, CreateDefaultProjection());
        registry.Characters.Add(record);
        SaveRegistry(player, registry);

        return RpCharacterOperationResult.Ok(Text("rpchar-success-created", Safe(record.DisplayName), record.CharacterId), record);
    }

    public RpCharacterOperationResult SelectCharacter(IServerPlayer player, string characterIdOrName)
    {
        if (player == null || string.IsNullOrWhiteSpace(player.PlayerUID))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-valid-online-player"));
        }

        if (!_swapLocks.Add(player.PlayerUID))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-switch-in-progress"));
        }

        try
        {
            var registry = EnsureRegistry(player);
            var target = FindCharacter(registry, characterIdOrName, includeArchived: false);
            if (target == null)
            {
                return RpCharacterOperationResult.Error(Text("rpchar-error-no-match", Safe(characterIdOrName)));
            }

            var activeId = GetActiveCharacterId(player);
            if (target.CharacterId.Equals(activeId, StringComparison.OrdinalIgnoreCase))
            {
                CaptureIntoRecord(player, target);
                SaveRegistry(player, registry);
                return RpCharacterOperationResult.Ok(Text("rpchar-success-already-active", Safe(target.DisplayName)), target);
            }

            var active = FindCharacter(registry, activeId, includeArchived: true);
            if (active != null)
            {
                CaptureIntoRecord(player, active);
            }

            RestoreProjection(player, target.Projection);
            SetActiveCharacterId(player, target.CharacterId);
            SaveRegistry(player, registry);
            player.ClearOutgoingTpaRequest();

            return RpCharacterOperationResult.Ok(Text("rpchar-success-switched", Safe(target.DisplayName)), target);
        }
        finally
        {
            _swapLocks.Remove(player.PlayerUID);
        }
    }

    public RpCharacterOperationResult RenameCharacter(IServerPlayer player, string characterIdOrName, string displayName)
    {
        var registry = EnsureRegistry(player);
        var target = FindCharacter(registry, characterIdOrName, includeArchived: false);
        if (target == null)
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-no-match", Safe(characterIdOrName)));
        }

        displayName = NormalizeDisplayName(displayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-name-required"));
        }

        if (registry.Characters.Any(character =>
                !character.Archived &&
                !character.CharacterId.Equals(target.CharacterId, StringComparison.OrdinalIgnoreCase) &&
                character.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-duplicate-name", Safe(displayName)));
        }

        target.DisplayName = displayName;
        target.ModifiedUtc = NowUtc();
        SaveRegistry(player, registry);
        return RpCharacterOperationResult.Ok(Text("rpchar-success-renamed", Safe(target.DisplayName)), target);
    }

    public RpCharacterOperationResult ArchiveCharacter(IServerPlayer player, string characterIdOrName)
    {
        var registry = EnsureRegistry(player);
        var target = FindCharacter(registry, characterIdOrName, includeArchived: false);
        if (target == null)
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-no-match", Safe(characterIdOrName)));
        }

        if (target.CharacterId.Equals(GetActiveCharacterId(player), StringComparison.OrdinalIgnoreCase))
        {
            return RpCharacterOperationResult.Error(Text("rpchar-error-archive-active"));
        }

        target.Archived = true;
        target.ModifiedUtc = NowUtc();
        SaveRegistry(player, registry);
        return RpCharacterOperationResult.Ok(Text("rpchar-success-archived", Safe(target.DisplayName)), target);
    }

    public void CaptureActiveProjection(IServerPlayer player)
    {
        if (player == null)
        {
            return;
        }

        var registry = ReadRegistry(player);
        if (registry.Characters.Count == 0)
        {
            return;
        }

        var active = FindCharacter(registry, GetActiveCharacterId(player), includeArchived: true);
        if (active == null)
        {
            return;
        }

        CaptureIntoRecord(player, active);
        SaveRegistry(player, registry);
    }

    public void RestoreProjection(IServerPlayer player, RpCharacterProjectionSnapshot projection)
    {
        projection ??= CreateDefaultProjection();
        NormalizeProjection(projection);

        IServerPlayerExtensions.SetModData(player, CharacterSheetKey, CloneSheet(projection.Sheet));
        if (string.IsNullOrWhiteSpace(projection.NicknameColor))
        {
            player.RemoveModdata(NicknameColorKey);
        }
        else
        {
            IServerPlayerExtensions.SetModData(player, NicknameColorKey, projection.NicknameColor);
        }

        player.SetLanguages(projection.Languages);
        IServerPlayerExtensions.SetModData(player, "BASIC_DEFAULT_LANGUAGE", projection.DefaultLanguage);
        player.SetChatMode(projection.ChatMode);
        player.SetChatterEnabled(projection.ChatterEnabled);
    }

    public RpCharacterProjectionSnapshot CaptureProjection(IServerPlayer player)
    {
        var projection = new RpCharacterProjectionSnapshot
        {
            Sheet = CloneSheet(IServerPlayerExtensions.GetModData(player, CharacterSheetKey, new CharacterSheetData()) ?? new CharacterSheetData()),
            NicknameColor = IServerPlayerExtensions.GetModData<string>(player, NicknameColorKey, null),
            Languages = player.GetLanguages().ToList(),
            DefaultLanguage = player.GetDefaultLanguageName(),
            ChatMode = player.GetChatMode(),
            ChatterEnabled = player.GetChatterEnabled()
        };

        NormalizeProjection(projection);
        return projection;
    }

    private void CaptureIntoRecord(IServerPlayer player, RpCharacterRecord record)
    {
        record.Projection = CaptureProjection(player);
        record.ModifiedUtc = NowUtc();
    }

    private void SaveRegistry(IServerPlayer player, RpCharacterRegistry registry)
    {
        IServerPlayerExtensions.SetModData(player, CharacterSlotsKey, registry);
    }

    private static void SetActiveCharacterId(IServerPlayer player, string characterId)
    {
        IServerPlayerExtensions.SetModData(player, ActiveCharacterIdKey, characterId ?? string.Empty);
    }

    private RpCharacterProjectionSnapshot CreateDefaultProjection()
    {
        var languages = GetDefaultLanguages();
        return new RpCharacterProjectionSnapshot
        {
            Sheet = new CharacterSheetData(),
            Languages = languages,
            DefaultLanguage = languages.FirstOrDefault() ?? LanguageSystem.BabbleLang.Name,
            ChatMode = ProximityChatMode.Normal,
            ChatterEnabled = true
        };
    }

    private void NormalizeProjection(RpCharacterProjectionSnapshot projection)
    {
        projection.Sheet = CloneSheet(projection.Sheet ?? new CharacterSheetData());
        projection.Languages = projection.Languages?
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (projection.Languages.Count == 0)
        {
            projection.Languages = GetDefaultLanguages();
        }

        if (string.IsNullOrWhiteSpace(projection.DefaultLanguage))
        {
            projection.DefaultLanguage = projection.Languages.FirstOrDefault() ?? LanguageSystem.BabbleLang.Name;
        }
    }

    private List<string> GetDefaultLanguages()
    {
        return (_config.Languages ?? new List<Language>())
            .Where(language => language.Default)
            .Select(language => language.Name)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private RpCharacterRecord CreateRecord(string characterId, string displayName, RpCharacterProjectionSnapshot projection)
    {
        var now = NowUtc();
        return new RpCharacterRecord
        {
            CharacterId = characterId,
            DisplayName = displayName,
            Projection = projection,
            CreatedUtc = now,
            ModifiedUtc = now
        };
    }

    private string GetDefaultDisplayName(IServerPlayer player)
    {
        var fullName = player.GetCharacterSheetFullName(_config)?.Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return NormalizeDisplayName(player.GetNickname(_config));
    }

    private static RpCharacterRecord FindCharacter(RpCharacterRegistry registry, string characterIdOrName, bool includeArchived)
    {
        var query = NormalizeDisplayName(characterIdOrName);
        if (registry == null || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var candidates = registry.Characters
            .Where(character => includeArchived || !character.Archived)
            .ToList();

        var exact = candidates.FirstOrDefault(character =>
            character.CharacterId.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            character.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        var normalizedQuery = NormalizeLookupText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        var normalizedMatches = candidates
            .Where(character =>
                NormalizeLookupText(character.CharacterId).Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                NormalizeLookupText(character.DisplayName).Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (normalizedMatches.Count == 1)
        {
            return normalizedMatches[0];
        }

        var idPrefixMatches = candidates
            .Where(character => NormalizeLookupText(character.CharacterId).StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return idPrefixMatches.Count == 1 ? idPrefixMatches[0] : null;
    }

    private static string GenerateCharacterId(RpCharacterRegistry registry, string seed)
    {
        var slug = NormalizeSlug(seed);
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "character";
        }

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var id = $"{slug}-{suffix}";
            if (registry.Characters.All(character => !character.CharacterId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                return id;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string NormalizeSlug(string value)
    {
        var characters = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        var slug = new string(characters);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }

    private static string NormalizeDisplayName(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string NormalizeLookupText(string value)
    {
        var text = NormalizeDisplayName(value);
        while (text.Length > 0 && IsQuote(text[0]))
        {
            text = text[1..].TrimStart();
        }

        while (text.Length > 0 && IsQuote(text[^1]))
        {
            text = text[..^1].TrimEnd();
        }

        return text;
    }

    private static bool IsQuote(char character)
    {
        return character == '"' || character == (char)39;
    }

    private static CharacterSheetData CloneSheet(CharacterSheetData data)
    {
        return new CharacterSheetData
        {
            Fields = (data?.Fields ?? new List<CharacterSheetStoredField>())
                .Where(field => field != null && !string.IsNullOrWhiteSpace(field.FieldId))
                .Select(field => new CharacterSheetStoredField
                {
                    FieldId = field.FieldId,
                    Value = field.Value ?? string.Empty
                })
                .ToList()
        };
    }

    private static string NowUtc()
    {
        return DateTime.UtcNow.ToString("O");
    }

    private string Text(string key, params object[] args)
    {
        return _localize(key, args ?? Array.Empty<object>());
    }

    private static string Safe(string value)
    {
        return VtmlUtils.EscapeVtml(value ?? string.Empty);
    }

    private static string EnglishText(string key, object[] args)
    {
        var template = EnglishTemplates.TryGetValue(key, out var value) ? value : key;

        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
