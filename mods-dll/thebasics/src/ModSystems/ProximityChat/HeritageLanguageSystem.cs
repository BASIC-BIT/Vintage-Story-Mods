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
    private readonly bool _playerModelLibEnabled;
    private readonly bool _hasModelBasedLanguages;

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
        _hasModelBasedLanguages = DetermineHasModelBasedLanguages();

        api.Event.PlayerJoin += HandlePlayerJoin;
    }

    private void HandlePlayerJoin(IServerPlayer player)
    {
        player.InstantiateLanguagesIfNotExist(Config);

        InitializeClassAndTraitHeritage(player);

        if (_playerModelLibEnabled && _hasModelBasedLanguages)
        {
            InitializeModelHeritage(player);
        }
    }

    private void InitializeClassAndTraitHeritage(IServerPlayer player)
    {
        var classCode = GetPlayerClass(player);
        if (!string.IsNullOrWhiteSpace(classCode))
        {
            GrantClassLanguages(player, classCode);
        }
        StorePlayerClass(player, classCode);

        var traitCodes = GetPlayerTraits(player).ToArray();
        if (traitCodes.Length > 0)
        {
            GrantTraitLanguages(player, traitCodes, notify: false);
        }
        StorePlayerTraits(player, traitCodes);

        RegisterAttributeWatcher(player, "characterClass", _classListeners, HandleClassChanged);
        RegisterAttributeWatcher(player, "extraTraits", _traitListeners, HandleTraitsChanged);

        RegisterModelWatcher(player);
    }

    private void RegisterAttributeWatcher(IServerPlayer player, string attributeKey, Dictionary<string, Action> registry, Action<IServerPlayer> handler)
    {
        var attributes = player.Entity?.WatchedAttributes;
        if (attributes == null)
        {
            return;
        }

        if (registry.ContainsKey(player.PlayerUID))
        {
            return;
        }

        void Listener() => handler(player);

        registry[player.PlayerUID] = Listener;
        attributes.RegisterModifiedListener(attributeKey, Listener);
    }

    private void HandleClassChanged(IServerPlayer player)
    {
        var attributes = player.Entity?.WatchedAttributes;
        if (attributes == null)
        {
            return;
        }

        var previousClass = player.GetModData<string?>(ClassModDataKey, null);
        var currentClass = GetPlayerClass(player);

        if (!string.IsNullOrWhiteSpace(previousClass) && !string.Equals(previousClass, currentClass, StringComparison.OrdinalIgnoreCase))
        {
            RemoveClassLanguages(player, previousClass);
        }

        if (!string.IsNullOrWhiteSpace(currentClass) && !string.Equals(previousClass, currentClass, StringComparison.OrdinalIgnoreCase))
        {
            GrantClassLanguages(player, currentClass); 
        }

        var previousTraits = player.GetModData<string[]?>(TraitsModDataKey, null) ?? Array.Empty<string>();
        var currentTraits = GetPlayerTraits(player).ToArray();

        UpdateTraitLanguages(player, previousTraits, currentTraits, notify: true);

        StorePlayerClass(player, currentClass);
        StorePlayerTraits(player, currentTraits);
    }

    private void HandleTraitsChanged(IServerPlayer player)
    {
        var previousTraits = player.GetModData<string[]?>(TraitsModDataKey, null) ?? Array.Empty<string>();
        var currentTraits = GetPlayerTraits(player).ToArray();

        UpdateTraitLanguages(player, previousTraits, currentTraits, notify: true);

        StorePlayerTraits(player, currentTraits);
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

    private void InitializeModelHeritage(IServerPlayer player)
    {
        var modelCode = GetPlayerModelCode(player);
        var modelGroupCode = GetModelGroupCode(modelCode);

        StorePlayerAppearance(player, modelCode ?? string.Empty, modelGroupCode ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(modelCode) || !string.IsNullOrWhiteSpace(modelGroupCode))
        {
            GrantModelLanguages(player, modelCode ?? string.Empty, modelGroupCode ?? string.Empty, notify: false);
        }

        RegisterModelWatcher(player);
    }

    private bool DetermineHasModelBasedLanguages()
    {
        return Config.Languages != null && Config.Languages.Any(language =>
            (language.GrantedToModels?.Length ?? 0) > 0 ||
            (language.GrantedToModelGroups?.Length ?? 0) > 0);
    }

    private void GrantClassLanguages(IServerPlayer player, string characterClass)
    {
        var bindings = BuildBindings(
            new HeritageBindingSpec(lang => lang.GrantedToClasses ?? Array.Empty<string>(), characterClass, false)
        ).ToArray();

        if (bindings.Length == 0)
        {
            return;
        }

        GrantBoundLanguages(
            player,
            bindings,
            language => L(ClassGainKey, characterClass, ChatHelper.LangIdentifier(language)));
    }

    private void RemoveClassLanguages(IServerPlayer player, string characterClass)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var bindings = BuildBindings(
            new HeritageBindingSpec(lang => lang.GrantedToClasses ?? Array.Empty<string>(), characterClass, false)
        ).ToArray();

        if (bindings.Length == 0)
        {
            return;
        }

        RemoveBoundLanguages(
            player,
            bindings,
            language => L(ClassLossKey, characterClass, ChatHelper.LangIdentifier(language)));
    }

    private void GrantTraitLanguages(IServerPlayer player, IEnumerable<string> traitCodes, bool notify)
    {
        var normalized = NormalizeCodes(traitCodes);
        if (normalized.Length == 0)
        {
            return;
        }

        var specs = normalized
            .Select(code => new HeritageBindingSpec(lang => lang.GrantedToTraits ?? Array.Empty<string>(), code, false))
            .ToArray();

        if (specs.Length == 0)
        {
            return;
        }

        var bindings = BuildBindings(specs).ToArray();
        if (bindings.Length == 0)
        {
            return;
        }

        var messageFactory = notify
            ? new System.Func<Language, string>(language => L(TraitGainKey, ChatHelper.LangIdentifier(language)))
            : null;

        GrantBoundLanguages(player, bindings, messageFactory);
    }

    private void RemoveTraitLanguages(IServerPlayer player, IEnumerable<string> traitCodes)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var normalized = NormalizeCodes(traitCodes);
        if (normalized.Length == 0)
        {
            return;
        }

        var specs = normalized
            .Select(code => new HeritageBindingSpec(lang => lang.GrantedToTraits ?? Array.Empty<string>(), code, false))
            .ToArray();

        if (specs.Length == 0)
        {
            return;
        }

        var bindings = BuildBindings(specs).ToArray();
        if (bindings.Length == 0)
        {
            return;
        }

        RemoveBoundLanguages(
            player,
            bindings,
            language => L(TraitLossKey, ChatHelper.LangIdentifier(language)));
    }

    private void GrantModelLanguages(IServerPlayer player, string modelCode, string modelGroupCode, bool notify)
    {
        var bindings = BuildBindings(
            new HeritageBindingSpec(lang => lang.GrantedToModels ?? Array.Empty<string>(), modelCode, true),
            new HeritageBindingSpec(lang => lang.GrantedToModelGroups ?? Array.Empty<string>(), modelGroupCode, true)
        ).ToArray();

        if (bindings.Length == 0)
        {
            return;
        }

        var modelDescriptor = GetModelDescriptor(modelCode, modelGroupCode);

        var messageFactory = notify
            ? new System.Func<Language, string>(language => L(ModelGainKey, modelDescriptor, ChatHelper.LangIdentifier(language)))
            : null;

        GrantBoundLanguages(player, bindings, messageFactory);
    }

    private void RemoveModelLanguages(IServerPlayer player, string modelCode, string modelGroupCode)
    {
        if (!Config.RemoveGrantedLanguagesOnChange)
        {
            return;
        }

        var bindings = BuildBindings(
            new HeritageBindingSpec(lang => lang.GrantedToModels ?? Array.Empty<string>(), modelCode, true),
            new HeritageBindingSpec(lang => lang.GrantedToModelGroups ?? Array.Empty<string>(), modelGroupCode, true)
        ).ToArray();

        if (bindings.Length == 0)
        {
            return;
        }

        var modelDescriptor = GetModelDescriptor(modelCode, modelGroupCode);

        RemoveBoundLanguages(
            player,
            bindings,
            language => L(ModelLossKey, modelDescriptor, ChatHelper.LangIdentifier(language)));
    }

    private void RegisterModelWatcher(IServerPlayer player)
    {
        if (!_playerModelLibEnabled || !_hasModelBasedLanguages)
        {
            return;
        }

        var attributes = player.Entity?.WatchedAttributes;
        if (attributes == null)
        {
            return;
        }

        if (_modelListeners.ContainsKey(player.PlayerUID))
        {
            return;
        }

        void Listener() => HandleSkinModelChanged(player);

        _modelListeners[player.PlayerUID] = Listener;
        attributes.RegisterModifiedListener("skinModel", Listener);
    }

    private void HandleSkinModelChanged(IServerPlayer player)
    {
        if (!_playerModelLibEnabled || !_hasModelBasedLanguages)
        {
            return;
        }

        var attributes = player.Entity?.WatchedAttributes;
        if (attributes == null)
        {
            return;
        }

        var newModel = GetPlayerModelCode(player);
        var newModelGroup = GetModelGroupCode(newModel);
        var previousModel = player.GetModData<string?>(ModelModDataKey, null);
        var previousModelGroup = player.GetModData<string?>(ModelGroupModDataKey, null);

        if (string.Equals(previousModel, newModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previousModelGroup, newModelGroup, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousModel) || !string.IsNullOrWhiteSpace(previousModelGroup))
        {
            RemoveModelLanguages(player, previousModel ?? string.Empty, previousModelGroup ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(newModel) || !string.IsNullOrWhiteSpace(newModelGroup))
        {
            GrantModelLanguages(player, newModel ?? string.Empty, newModelGroup ?? string.Empty, notify: true);
        }

        StorePlayerAppearance(player, newModel ?? string.Empty, newModelGroup ?? string.Empty);
    }

    private static string GetPlayerModelCode(IServerPlayer player)
    {
        return player.Entity?.WatchedAttributes?.GetString("skinModel") ?? string.Empty;
    }

    private string GetModelGroupCode(string modelCode)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return string.Empty;
        }

        if (!_playerModelLibEnabled)
        {
            return string.Empty;
        }

        if (!TryEnsureCustomModelReflection())
        {
            return string.Empty;
        }

        if (_customModelsProperty?.GetValue(_customModelsSystem) is IDictionary models &&
            models.Contains(modelCode))
        {
            var modelData = models[modelCode];
            return _groupProperty?.GetValue(modelData) as string ?? string.Empty;
        }

        return string.Empty;
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

    private static string GetPlayerClass(IServerPlayer player)
    {
        return player.Entity?.WatchedAttributes?.GetString("characterClass") ?? string.Empty;
    }

    private IEnumerable<string> GetPlayerTraits(IServerPlayer player)
    {
        var traitCodes = new List<string>();

        var classCode = GetPlayerClass(player);
        if (!string.IsNullOrWhiteSpace(classCode))
        {
            traitCodes.AddRange(GetClassTraits(classCode));
        }

        var extraTraits = player.Entity?.WatchedAttributes?.GetStringArray("extraTraits");
        if (extraTraits != null)
        {
            traitCodes.AddRange(extraTraits);
        }

        return NormalizeCodes(traitCodes);
    }

    private string[] GetClassTraits(string classCode)
    {
        if (string.IsNullOrWhiteSpace(classCode))
        {
            return Array.Empty<string>();
        }

        EnsureClassTraitCache();

        if (_classTraitCache.TryGetValue(classCode, out var traits))
        {
            return traits;
        }

        return Array.Empty<string>();
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
            if (characterSystem?.characterClassesByCode != null && characterSystem.characterClassesByCode.Count > 0)
            {
                foreach (var kvp in characterSystem.characterClassesByCode)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        continue;
                    }

                    _classTraitCache[kvp.Key] = kvp.Value?.Traits ?? Array.Empty<string>();
                }

                return;
            }
        }
        catch
        {
            // Ignore and fall back to asset loading
        }

        try
        {
            var classes = API.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();
            if (classes != null)
            {
                foreach (var characterClass in classes)
                {
                    if (string.IsNullOrWhiteSpace(characterClass.Code))
                    {
                        continue;
                    }

                    _classTraitCache[characterClass.Code] = characterClass.Traits ?? Array.Empty<string>();
                }
            }
        }
        catch
        {
            // Silently ignore if the asset cannot be loaded
        }
    }

    private static string[] NormalizeCodes(IEnumerable<string> codes)
    {
        return codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void GrantBoundLanguages(IServerPlayer player, IEnumerable<LanguageBinding> bindings, System.Func<Language, string>? messageFactory)
    {
        foreach (var language in SelectBoundLanguages(bindings))
        {
            if (player.KnowsLanguage(language))
            {
                continue;
            }

            player.AddLanguage(language);

            var message = messageFactory?.Invoke(language);
            if (!string.IsNullOrWhiteSpace(message))
            {
                player.SendMessage(
                    GlobalConstants.CurrentChatGroup,
                    message,
                    EnumChatType.Notification);
            }

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
    }

    private void RemoveBoundLanguages(IServerPlayer player, IEnumerable<LanguageBinding> bindings, System.Func<Language, string> messageFactory)
    {
        var removed = false;

        foreach (var language in SelectBoundLanguages(bindings))
        {
            if (!player.KnowsLanguage(language))
            {
                continue;
            }

            player.RemoveLanguage(language);
            removed = true;

            var message = messageFactory(language);
            if (!string.IsNullOrWhiteSpace(message))
            {
                player.SendMessage(
                    GlobalConstants.CurrentChatGroup,
                    message,
                    EnumChatType.Notification);
            }
        }

        if (removed)
        {
            EnsureValidDefaultLanguage(player);
        }
    }

    private IEnumerable<Language> SelectBoundLanguages(IEnumerable<LanguageBinding> bindings)
    {
        var bindingList = bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Code))
            .ToList();

        if (bindingList.Count == 0)
        {
            yield break;
        }

        if (Config.Languages == null || Config.Languages.Count == 0)
        {
            yield break;
        }

        var seen = new HashSet<Language>();

        foreach (var language in Config.Languages)
        {
            if (bindingList.Any(binding => BindingMatches(language, binding)) &&
                seen.Add(language))
            {
                yield return language;
            }
        }
    }

    private static bool BindingMatches(Language language, LanguageBinding binding)
    {
        var values = binding.Selector?.Invoke(language);
        if (values == null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (binding.AllowWildcard)
            {
                if (PatternMatchUtils.WildcardMatches(binding.Code, value))
                {
                    return true;
                }
            }
            else if (string.Equals(value, binding.Code, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct HeritageBindingSpec
    {
        public HeritageBindingSpec(System.Func<Language, IEnumerable<string>> selector, string code, bool allowWildcard)
        {
            Selector = selector;
            Code = code;
            AllowWildcard = allowWildcard;
        }

        public System.Func<Language, IEnumerable<string>> Selector { get; }
        public string Code { get; }
        public bool AllowWildcard { get; }
    }

    private readonly struct LanguageBinding
    {
        public LanguageBinding(System.Func<Language, IEnumerable<string>> selector, string code, bool allowWildcard)
        {
            Selector = selector ?? (_ => Array.Empty<string>());
            Code = code ?? string.Empty;
            AllowWildcard = allowWildcard;
        }

        public System.Func<Language, IEnumerable<string>> Selector { get; }
        public string Code { get; }
        public bool AllowWildcard { get; }
    }

    private static IEnumerable<LanguageBinding> BuildBindings(params HeritageBindingSpec[] specifications)
    {
        return BuildBindings((IEnumerable<HeritageBindingSpec>)specifications);
    }

    private static IEnumerable<LanguageBinding> BuildBindings(IEnumerable<HeritageBindingSpec> specifications)
    {
        foreach (var spec in specifications)
        {
            if (string.IsNullOrWhiteSpace(spec.Code))
            {
                continue;
            }

            yield return new LanguageBinding(spec.Selector, spec.Code, spec.AllowWildcard);
        }
    }

    private void EnsureValidDefaultLanguage(IServerPlayer player)
    {
        var replacement = LanguageSystem.BabbleLang;

        try
        {
            var currentDefault = player.GetDefaultLanguage(Config);
            if (player.KnowsLanguage(currentDefault))
            {
                return;
            }
        }
        catch
        {
            // Ignore and fallback to replacement logic
        }

        var knownLanguages = player.GetLanguages();

        if (knownLanguages != null && knownLanguages.Count > 0 && Config.Languages != null)
        {
            var configuredLanguage = Config.Languages.FirstOrDefault(lang => knownLanguages.Contains(lang.Name));
            if (configuredLanguage != null)
            {
                replacement = configuredLanguage;
            }
        }

        player.SetDefaultLanguage(replacement);
    }

    private bool TryEnsureCustomModelReflection()
    {
        if (_customModelsSystem != null &&
            _customModelsProperty != null &&
            _groupProperty != null)
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
            if (customModelDataType == null)
            {
                ResetReflectionCache();
                return false;
            }

            _groupProperty = customModelDataType.GetProperty("Group");
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

    private static IEnumerable<HeritageBindingSpec> EnumerateModelBindingSpecifications(string modelCode, string modelGroupCode)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var variant in ExpandModelCodeVariants(modelCode))
        {
            if (seen.Add(variant))
            {
                yield return new HeritageBindingSpec(lang => lang.GrantedToModels ?? Array.Empty<string>(), variant, true);
            }
        }

        foreach (var variant in ExpandModelCodeVariants(modelGroupCode))
        {
            if (seen.Add("group::" + variant))
            {
                yield return new HeritageBindingSpec(lang => lang.GrantedToModelGroups ?? Array.Empty<string>(), variant, true);
            }
        }
    }

    private static IEnumerable<string> ExpandModelCodeVariants(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            yield break;
        }

        yield return code;

        int separator = code.IndexOf(':');
        if (separator >= 0 && separator < code.Length - 1)
        {
            var withoutDomain = code[(separator + 1)..];
            if (!string.IsNullOrWhiteSpace(withoutDomain))
            {
                yield return withoutDomain;
            }
        }
    }
}

