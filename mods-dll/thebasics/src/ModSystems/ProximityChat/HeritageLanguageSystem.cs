#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ProximityChat;

public class HeritageLanguageSystem : BaseSubSystem
{
    private const string ClassGainKey = "heritage.class.gain";
    private const string ClassLossKey = "heritage.class.loss";
    private const string TraitGainKey = "heritage.trait.gain";
    private const string TraitLossKey = "heritage.trait.loss";
    private const string ModelGainKey = "heritage.model.gain";
    private const string ModelLossKey = "heritage.model.loss";

    private const string ModelModDataKey = "BASIC_LAST_MODEL_LANGUAGE";
    private const string ModelGroupModDataKey = "BASIC_LAST_MODEL_GROUP_LANGUAGE";
    private const string ClassModDataKey = "BASIC_LAST_CLASS_LANGUAGE";
    private const string TraitsModDataKey = "BASIC_LAST_TRAITS_LANGUAGE";

    private readonly Dictionary<string, Action> _modelListeners = new();
    private readonly Dictionary<string, Action> _classListeners = new();
    private readonly Dictionary<string, Action> _traitListeners = new();
    private readonly object _mutationLock = new();
    private readonly bool _playerModelLibEnabled;
    private readonly bool _hasModelBindings;

    private bool _classTraitsCacheInitialized;
    private readonly Dictionary<string, string[]> _classTraitCache = new(StringComparer.OrdinalIgnoreCase);

    private object? _customModelsSystem;
    private PropertyInfo? _customModelsProperty;
    private PropertyInfo? _groupProperty;

    private static string L(string key, params object[] args) => Lang.Get($"thebasics:{key}", args);

    public HeritageLanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config, bool playerModelLibEnabled)
        : base(system, api, config)
    {
        _playerModelLibEnabled = playerModelLibEnabled;
        _hasModelBindings = Config.Languages != null && Config.Languages.Any(language =>
            language.GrantedToModels.Length > 0 ||
            language.GrantedToModelGroups.Length > 0);

        api.Event.PlayerJoin += HandlePlayerJoin;
        api.Event.PlayerDisconnect += HandlePlayerDisconnect;
    }

    private void HandlePlayerJoin(IServerPlayer player)
    {
        player.InstantiateLanguagesIfNotExist(Config);

        var classCode = GetPlayerClass(player);
        if (!string.IsNullOrWhiteSpace(classCode))
        {
            GrantClassLanguages(player, classCode, notify: true);
        }
        StorePlayerClass(player, classCode);

        var traits = GetPlayerTraits(player);
        if (traits.Length > 0)
        {
            GrantTraitLanguages(player, traits, notify: true);
        }
        StorePlayerTraits(player, traits);

        RegisterAttributeWatcher(player, "characterClass", _classListeners, HandleClassChanged);
        RegisterAttributeWatcher(player, "extraTraits", _traitListeners, HandleTraitsChanged);

        if (_playerModelLibEnabled && _hasModelBindings)
        {
            var modelCode = GetPlayerModelCode(player);
            var modelGroup = GetModelGroupCode(modelCode);

            if (!string.IsNullOrWhiteSpace(modelCode) || !string.IsNullOrWhiteSpace(modelGroup))
            {
                GrantModelLanguages(player, modelCode, modelGroup, notify: true);
            }

            StorePlayerAppearance(player, modelCode, modelGroup);
            RegisterModelWatcher(player);
        }
    }

    private void HandlePlayerDisconnect(IServerPlayer player)
    {
        if (string.IsNullOrWhiteSpace(player?.PlayerUID))
        {
            return;
        }

        _classListeners.Remove(player.PlayerUID);
        _traitListeners.Remove(player.PlayerUID);
        _modelListeners.Remove(player.PlayerUID);
    }

    private void RegisterAttributeWatcher(IServerPlayer player, string attributeKey, Dictionary<string, Action> registry, Action<IServerPlayer> callback)
    {
        var watchedAttributes = player.Entity?.WatchedAttributes;
        if (watchedAttributes == null || registry.ContainsKey(player.PlayerUID))
        {
            return;
        }

        void Listener() => callback(player);

        registry[player.PlayerUID] = Listener;
        watchedAttributes.RegisterModifiedListener(attributeKey, Listener);
    }

    private void HandleClassChanged(IServerPlayer player)
    {
        lock (_mutationLock)
        {
            var previousClass = player.GetModData<string?>(ClassModDataKey, null) ?? string.Empty;
            var currentClass = GetPlayerClass(player);

            if (!string.Equals(previousClass, currentClass, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(previousClass))
                {
                    RemoveClassLanguages(player, previousClass);
                }

                if (!string.IsNullOrWhiteSpace(currentClass))
                {
                    GrantClassLanguages(player, currentClass, notify: true);
                }
            }

            var previousTraits = player.GetModData<string[]?>(TraitsModDataKey, null) ?? Array.Empty<string>();
            var currentTraits = GetPlayerTraits(player);
            if (!AreSameCodes(previousTraits, currentTraits))
            {
                UpdateTraitLanguages(player, previousTraits, currentTraits, notify: true);
            }

            StorePlayerClass(player, currentClass);
            StorePlayerTraits(player, currentTraits);
        }
    }

    public void ReconcilePlayerClassChange(IServerPlayer player)
    {
        HandleClassChanged(player);
    }

    private void HandleTraitsChanged(IServerPlayer player)
    {
        lock (_mutationLock)
        {
            var previousTraits = player.GetModData<string[]?>(TraitsModDataKey, null) ?? Array.Empty<string>();
            var currentTraits = GetPlayerTraits(player);
            if (AreSameCodes(previousTraits, currentTraits))
            {
                return;
            }

            UpdateTraitLanguages(player, previousTraits, currentTraits, notify: true);
            StorePlayerTraits(player, currentTraits);
        }
    }

    private void UpdateTraitLanguages(IServerPlayer player, string[] previousTraits, string[] currentTraits, bool notify)
    {
        var previous = NormalizeCodes(previousTraits);
        var current = NormalizeCodes(currentTraits);

        var removed = previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        var added = current.Except(previous, StringComparer.OrdinalIgnoreCase).ToArray();

        if (removed.Length > 0)
        {
            RemoveTraitLanguages(player, removed);
        }

        if (added.Length > 0)
        {
            GrantTraitLanguages(player, added, notify);
        }
    }

    private void GrantClassLanguages(IServerPlayer player, string classCode, bool notify)
    {
        var toGrant = Config.Languages
            .Where(language => language.GrantedToClasses.Any(bound => string.Equals(bound, classCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        GrantLanguages(
            player,
            toGrant,
            notify ? language => L(ClassGainKey, classCode, ChatHelper.LangIdentifier(language)) : null);
    }

    private void RemoveClassLanguages(IServerPlayer player, string classCode)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var currentClass = GetPlayerClass(player);
        var currentTraits = GetPlayerTraits(player);
        var (currentModelCode, currentModelGroupCode) = GetCurrentModelContext(player);

        var toRemove = Config.Languages
            .Where(language => language.GrantedToClasses.Any(bound => string.Equals(bound, classCode, StringComparison.OrdinalIgnoreCase)))
            .Where(language => !IsLanguageGrantedByHeritage(language, currentClass, currentTraits, currentModelCode, currentModelGroupCode))
            .ToList();

        RemoveLanguages(
            player,
            toRemove,
            language => L(ClassLossKey, classCode, ChatHelper.LangIdentifier(language)));
    }

    private void GrantTraitLanguages(IServerPlayer player, IEnumerable<string> traitCodes, bool notify)
    {
        var traitSet = new HashSet<string>(NormalizeCodes(traitCodes), StringComparer.OrdinalIgnoreCase);

        var toGrant = Config.Languages
            .Where(language => language.GrantedToTraits.Any(trait => traitSet.Contains(trait)))
            .ToList();

        GrantLanguages(
            player,
            toGrant,
            notify ? language => L(TraitGainKey, ChatHelper.LangIdentifier(language)) : null);
    }

    private void RemoveTraitLanguages(IServerPlayer player, IEnumerable<string> traitCodes)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var traitSet = new HashSet<string>(NormalizeCodes(traitCodes), StringComparer.OrdinalIgnoreCase);
        var currentClass = GetPlayerClass(player);
        var currentTraits = GetPlayerTraits(player);
        var (currentModelCode, currentModelGroupCode) = GetCurrentModelContext(player);

        var toRemove = Config.Languages
            .Where(language => language.GrantedToTraits.Any(trait => traitSet.Contains(trait)))
            .Where(language => !IsLanguageGrantedByHeritage(language, currentClass, currentTraits, currentModelCode, currentModelGroupCode))
            .ToList();

        RemoveLanguages(
            player,
            toRemove,
            language => L(TraitLossKey, ChatHelper.LangIdentifier(language)));
    }

    private void GrantModelLanguages(IServerPlayer player, string modelCode, string modelGroup, bool notify)
    {
        var modelVariants = ExpandModelCodeVariants(modelCode).ToArray();
        var groupVariants = ExpandModelCodeVariants(modelGroup).ToArray();

        var toGrant = Config.Languages
            .Where(language =>
                MatchesAny(language.GrantedToModels, modelVariants) ||
                MatchesAny(language.GrantedToModelGroups, groupVariants))
            .ToList();

        GrantLanguages(
            player,
            toGrant,
            notify ? language => L(ModelGainKey, GetDescriptorForLanguage(language, modelCode, modelGroup, modelVariants, groupVariants), ChatHelper.LangIdentifier(language)) : null);
    }

    private void RemoveModelLanguages(IServerPlayer player, string modelCode, string modelGroup)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var currentClass = GetPlayerClass(player);
        var currentTraits = GetPlayerTraits(player);
        var (currentModelCode, currentModelGroupCode) = GetCurrentModelContext(player);

        var modelVariants = ExpandModelCodeVariants(modelCode).ToArray();
        var groupVariants = ExpandModelCodeVariants(modelGroup).ToArray();

        var toRemove = Config.Languages
            .Where(language =>
                MatchesAny(language.GrantedToModels, modelVariants) ||
                MatchesAny(language.GrantedToModelGroups, groupVariants))
            .Where(language => !IsLanguageGrantedByHeritage(language, currentClass, currentTraits, currentModelCode, currentModelGroupCode))
            .ToList();

        RemoveLanguages(
            player,
            toRemove,
            language => L(ModelLossKey, GetDescriptorForLanguage(language, modelCode, modelGroup, modelVariants, groupVariants), ChatHelper.LangIdentifier(language)));
    }

    private void RegisterModelWatcher(IServerPlayer player)
    {
        var watchedAttributes = player.Entity?.WatchedAttributes;
        if (watchedAttributes == null || _modelListeners.ContainsKey(player.PlayerUID))
        {
            return;
        }

        void Listener() => HandleSkinModelChanged(player);

        _modelListeners[player.PlayerUID] = Listener;
        watchedAttributes.RegisterModifiedListener("skinModel", Listener);
    }

    private void HandleSkinModelChanged(IServerPlayer player)
    {
        lock (_mutationLock)
        {
            var newModel = GetPlayerModelCode(player);
            var newModelGroup = GetModelGroupCode(newModel);
            var previousModel = player.GetModData<string?>(ModelModDataKey, null) ?? string.Empty;
            var previousModelGroup = player.GetModData<string?>(ModelGroupModDataKey, null) ?? string.Empty;

            if (string.Equals(previousModel, newModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousModelGroup, newModelGroup, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(previousModel) || !string.IsNullOrWhiteSpace(previousModelGroup))
            {
                RemoveModelLanguages(player, previousModel, previousModelGroup);
            }

            if (!string.IsNullOrWhiteSpace(newModel) || !string.IsNullOrWhiteSpace(newModelGroup))
            {
                GrantModelLanguages(player, newModel, newModelGroup, notify: true);
            }

            StorePlayerAppearance(player, newModel, newModelGroup);
        }
    }

    private static string GetPlayerClass(IServerPlayer player)
    {
        return player.Entity?.WatchedAttributes?.GetString("characterClass") ?? string.Empty;
    }

    private string[] GetPlayerTraits(IServerPlayer player)
    {
        var traits = new List<string>();

        var classCode = GetPlayerClass(player);
        if (!string.IsNullOrWhiteSpace(classCode))
        {
            traits.AddRange(GetClassTraits(classCode));
        }

        var extraTraits = player.Entity?.WatchedAttributes?.GetStringArray("extraTraits");
        if (extraTraits != null)
        {
            traits.AddRange(extraTraits);
        }

        return NormalizeCodes(traits);
    }

    private string[] GetClassTraits(string classCode)
    {
        if (string.IsNullOrWhiteSpace(classCode))
        {
            return Array.Empty<string>();
        }

        EnsureClassTraitCache();
        return _classTraitCache.TryGetValue(classCode, out var traits)
            ? traits
            : Array.Empty<string>();
    }

    private void EnsureClassTraitCache()
    {
        if (_classTraitsCacheInitialized)
        {
            return;
        }

        _classTraitsCacheInitialized = true;

        try
        {
            var characterSystem = API.ModLoader.GetModSystem<CharacterSystem>();
            if (characterSystem?.characterClassesByCode != null)
            {
                foreach (var kvp in characterSystem.characterClassesByCode)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        _classTraitCache[kvp.Key] = kvp.Value?.Traits ?? Array.Empty<string>();
                    }
                }

                if (_classTraitCache.Count > 0)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            API.Logger.Warning("[thebasics] HeritageLanguageSystem: failed loading class traits from CharacterSystem ({0}), falling back to asset load.", ex.Message);
            // Fall back to asset loading.
        }

        try
        {
            var classes = API.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();
            if (classes == null)
            {
                return;
            }

            foreach (var characterClass in classes)
            {
                if (!string.IsNullOrWhiteSpace(characterClass.Code))
                {
                    _classTraitCache[characterClass.Code] = characterClass.Traits ?? Array.Empty<string>();
                }
            }
        }
        catch (Exception ex)
        {
            API.Logger.Warning("[thebasics] HeritageLanguageSystem: failed loading class traits from asset fallback ({0}).", ex.Message);
            // No class trait metadata available.
        }
    }

    private void GrantLanguages(IServerPlayer player, IEnumerable<Language> languages, System.Func<Language, string>? messageFactory)
    {
        foreach (var language in languages)
        {
            if (player.KnowsLanguage(language))
            {
                continue;
            }

            player.AddLanguage(language);

            var message = messageFactory?.Invoke(language);
            if (!string.IsNullOrWhiteSpace(message))
            {
                player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
            }

            TryPromoteDefaultLanguage(player, language);
        }
    }

    private void RemoveLanguages(IServerPlayer player, IEnumerable<Language> languages, System.Func<Language, string> messageFactory)
    {
        var removedAny = false;

        foreach (var language in languages)
        {
            if (!player.KnowsLanguage(language))
            {
                continue;
            }

            player.RemoveLanguage(language);
            removedAny = true;

            var message = messageFactory(language);
            if (!string.IsNullOrWhiteSpace(message))
            {
                player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
            }
        }

        if (removedAny)
        {
            EnsureValidDefaultLanguage(player);
        }
    }

    private void TryPromoteDefaultLanguage(IServerPlayer player, Language language)
    {
        try
        {
            if (player.GetDefaultLanguage(Config).Name == LanguageSystem.BabbleLang.Name)
            {
                player.SetDefaultLanguage(language);
            }
        }
        catch
        {
            player.SetDefaultLanguage(language);
        }
    }

    private void EnsureValidDefaultLanguage(IServerPlayer player)
    {
        try
        {
            var defaultLanguage = player.GetDefaultLanguage(Config);
            if (player.KnowsLanguage(defaultLanguage))
            {
                return;
            }
        }
        catch
        {
            // Fallback below.
        }

        var knownLanguageNames = player.GetLanguages();
        var firstKnownConfigured = Config.Languages.FirstOrDefault(language => knownLanguageNames.Contains(language.Name));
        player.SetDefaultLanguage(firstKnownConfigured ?? LanguageSystem.BabbleLang);
    }

    private string GetModelDescriptor(string modelCode, string modelGroupCode)
    {
        if (!string.IsNullOrWhiteSpace(modelGroupCode))
        {
            return Lang.Get("thebasics:heritage.model.groupDescriptor", modelGroupCode);
        }

        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            return Lang.Get("thebasics:heritage.model.modelDescriptor", modelCode);
        }

        return Lang.Get("thebasics:heritage.model.unknownDescriptor");
    }

    /// <summary>
    /// Returns the appropriate descriptor for a language based on whether it matched
    /// via GrantedToModels (model-specific) or GrantedToModelGroups (group-level).
    /// Prefers the model-specific descriptor when the language matched on the model directly.
    /// </summary>
    private string GetDescriptorForLanguage(Language language, string modelCode, string modelGroupCode, string[] modelVariants, string[] groupVariants)
    {
        var matchesModel = MatchesAny(language.GrantedToModels, modelVariants);
        var matchesGroup = MatchesAny(language.GrantedToModelGroups, groupVariants);

        if (matchesModel && !matchesGroup)
        {
            return GetModelDescriptor(modelCode, string.Empty);
        }

        if (matchesGroup && !matchesModel)
        {
            return GetModelDescriptor(string.Empty, modelGroupCode);
        }

        // Matched both — fall back to the default priority (group > model).
        return GetModelDescriptor(modelCode, modelGroupCode);
    }

    private static string GetPlayerModelCode(IServerPlayer player)
    {
        return player.Entity?.WatchedAttributes?.GetString("skinModel") ?? string.Empty;
    }

    private string GetModelGroupCode(string modelCode)
    {
        if (string.IsNullOrWhiteSpace(modelCode) || !_playerModelLibEnabled)
        {
            return string.Empty;
        }

        if (!TryEnsureCustomModelReflection())
        {
            return string.Empty;
        }

        if (_customModelsProperty?.GetValue(_customModelsSystem) is IDictionary models && models.Contains(modelCode))
        {
            var modelData = models[modelCode];
            return _groupProperty?.GetValue(modelData) as string ?? string.Empty;
        }

        return string.Empty;
    }

    private bool TryEnsureCustomModelReflection()
    {
        if (_customModelsSystem != null && _customModelsProperty != null && _groupProperty != null)
        {
            return true;
        }

        try
        {
            _customModelsSystem = API.ModLoader.GetModSystem("PlayerModelLib.CustomModelsSystem");
            if (_customModelsSystem == null)
            {
                return false;
            }

            _customModelsProperty = _customModelsSystem.GetType().GetProperty("CustomModels");
            if (_customModelsProperty == null)
            {
                ResetReflectionCache();
                return false;
            }

            var customModelDataType = Type.GetType("PlayerModelLib.CustomModelData, PlayerModelLib");
            _groupProperty = customModelDataType?.GetProperty("Group");
            if (_groupProperty == null)
            {
                ResetReflectionCache();
                return false;
            }

            return true;
        }
        catch
        {
            ResetReflectionCache();
            return false;
        }
    }

    private void ResetReflectionCache()
    {
        _customModelsSystem = null;
        _customModelsProperty = null;
        _groupProperty = null;
    }

    private void StorePlayerAppearance(IServerPlayer player, string modelCode, string modelGroupCode)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            player.RemoveModdata(ModelModDataKey);
        }
        else
        {
            player.SetModData(ModelModDataKey, modelCode);
        }

        if (string.IsNullOrWhiteSpace(modelGroupCode))
        {
            player.RemoveModdata(ModelGroupModDataKey);
        }
        else
        {
            player.SetModData(ModelGroupModDataKey, modelGroupCode);
        }
    }

    private void StorePlayerClass(IServerPlayer player, string? classCode)
    {
        if (string.IsNullOrWhiteSpace(classCode))
        {
            player.RemoveModdata(ClassModDataKey);
        }
        else
        {
            player.SetModData(ClassModDataKey, classCode);
        }
    }

    private void StorePlayerTraits(IServerPlayer player, string[] traitCodes)
    {
        if (traitCodes.Length == 0)
        {
            player.RemoveModdata(TraitsModDataKey);
        }
        else
        {
            player.SetModData(TraitsModDataKey, traitCodes);
        }
    }

    private (string modelCode, string modelGroupCode) GetCurrentModelContext(IServerPlayer player)
    {
        if (!_playerModelLibEnabled || !_hasModelBindings)
        {
            return (string.Empty, string.Empty);
        }

        var modelCode = GetPlayerModelCode(player);
        var modelGroupCode = GetModelGroupCode(modelCode);
        return (modelCode, modelGroupCode);
    }

    private bool IsLanguageGrantedByHeritage(Language language, string classCode, IEnumerable<string> traits, string modelCode, string modelGroupCode)
    {
        if (!string.IsNullOrWhiteSpace(classCode) && language.GrantedToClasses.Any(bound => string.Equals(bound, classCode, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var traitSet = new HashSet<string>(NormalizeCodes(traits), StringComparer.OrdinalIgnoreCase);
        if (traitSet.Count > 0 && language.GrantedToTraits.Any(traitSet.Contains))
        {
            return true;
        }

        if (!_playerModelLibEnabled || !_hasModelBindings)
        {
            return false;
        }

        var modelVariants = ExpandModelCodeVariants(modelCode).ToArray();
        var groupVariants = ExpandModelCodeVariants(modelGroupCode).ToArray();
        return MatchesAny(language.GrantedToModels, modelVariants) ||
               MatchesAny(language.GrantedToModelGroups, groupVariants);
    }

    private static bool AreSameCodes(IEnumerable<string> left, IEnumerable<string> right)
    {
        var normalizedLeft = NormalizeCodes(left);
        var normalizedRight = NormalizeCodes(right);

        return normalizedLeft.Length == normalizedRight.Length &&
               !normalizedLeft.Except(normalizedRight, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static bool MatchesAny(IEnumerable<string> patterns, IEnumerable<string> candidates)
    {
        var patternList = patterns?.Where(pattern => !string.IsNullOrWhiteSpace(pattern)).ToArray() ?? Array.Empty<string>();
        var candidateList = candidates?.Where(candidate => !string.IsNullOrWhiteSpace(candidate)).ToArray() ?? Array.Empty<string>();

        if (patternList.Length == 0 || candidateList.Length == 0)
        {
            return false;
        }

        foreach (var pattern in patternList)
        {
            foreach (var candidate in candidateList)
            {
                if (PatternMatchUtils.WildcardMatches(candidate, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> ExpandModelCodeVariants(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Array.Empty<string>();
        }

        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            code.Trim(),
        };

        var separator = code.IndexOf(':');
        if (separator >= 0 && separator < code.Length - 1)
        {
            variants.Add(code[(separator + 1)..]);
        }

        return variants;
    }

    private static string[] NormalizeCodes(IEnumerable<string> codes)
    {
        return codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
