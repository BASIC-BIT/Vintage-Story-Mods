using System.Reflection;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TheBasics.GuiPreview;

/// <summary>Fixtures instantiate production dialogs. Only data and environmental state are supplied here.</summary>
public sealed class PreviewScene : IDisposable
{
    private readonly Action restore;
    public GuiDialog Dialog { get; }
    public string Name { get; }
    private PreviewScene(string name, GuiDialog dialog, Action? restore = null)
    { Name = name; Dialog = dialog; this.restore = restore ?? (() => { }); }

    public static string[] Names => new[] { "sheet-fields-disabled-tooltip", "sheet-fields-private-tooltip", "sheet-fields-short-text", "sheet-fields-long-text", "sheet-fields-number", "sheet-fields-private", "sheet-fields", "sheet-fields-long-list", "sheet-preferences", "sheet-preferences-dropdown", "language-default", "language-focus", "language-dropdown", "language-hover", "language-tooltip", "language-error", "language-color-palette", "language-color-custom", "admin-chat", "admin-bubbles", "admin-discord", "admin-search", "admin-search-empty", "admin-search-none", "admin-search-target", "admin-chat-colors", "admin-chat-colors-custom", "admin-nametag-colors", "scene-marker-nearby", "scene-marker-nearby-on", "scene-marker-colors", "scene-marker-custom-color", "scene-marker-custom-picker", "scene-marker-custom-icon", "scene-marker-custom-locked" }.Concat(SetupWizardCaptureScenes.Names).ToArray();

    public string Coverage => Name.StartsWith("wizard-", StringComparison.Ordinal) ? "standalone-gui-and-seraph" : "standalone-gui";
    public string[] Omissions => Coverage == "standalone-gui-and-seraph" ? ["Live world rendering and GPU shader calibration are not exercised."] : [];
    public object? Fixture => Dialog is SetupWizardDialog wizard
        ? new { values = wizard.Draft.Values, languages = wizard.Draft.Languages, sheetFields = wizard.Draft.SheetFields }
        : null;

    public static PreviewScene Create(string name, PreviewHost host)
    {
        if (!Names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown GUI fixture: " + name);
        if (name.StartsWith("wizard-", StringComparison.Ordinal))
        {
            var wizard = new SetupWizardDialog(host.Api, new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), SetupWizardCaptureScenes.LanguagesForScenario(name), SetupWizardCaptureScenes.SheetFieldsForScenario(name)),
                "standalone-capture", name != "wizard-restart-integrated", () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true,
                actorFactory: host.CreateGuide);
            wizard.TryOpen();
            SetupWizardCaptureScenes.Show(wizard, name, SetupWizardCaptureScenes.DefaultPreviewTime(name));
            return new PreviewScene(name, wizard);
        }
        if (name.StartsWith("sheet-fields", StringComparison.Ordinal))
        {
            var entries = Enumerable.Range(1, name == "sheet-fields-long-list" ? 16 : 2).Select(index => new CharacterSheetFieldConfigEntryMessage
                { OriginalId = "field" + index, Id = "field" + index, Label = index == 1 ? "Occupation" : "Field " + index,
                  Type = name == "sheet-fields-long-text" ? "longstring" : name == "sheet-fields-short-text" ? "string" : name == "sheet-fields-number" ? "number" : "option", MaxLength = "128", EditorRows = "6", Options = "Scribe, Glassblower, Herbalist", Description = "The work your character does in the settlement.",
                  LayoutSection = name.StartsWith("sheet-fields-private", StringComparison.Ordinal) ? "header-side" : "body", Width = "full", Visibility = name.StartsWith("sheet-fields-private", StringComparison.Ordinal) ? "self" : "public", Optional = true }).ToList();
            var editor = new CharacterSheetFieldConfigDialog(host.Api, entries, _ => { }, () => { }, () => { });
            editor.TryOpen();
            var fieldScene = new PreviewScene(name, editor);
            if (name.EndsWith("tooltip", StringComparison.Ordinal))
            {
                host.RenderGuiDialog(editor, 0);
                fieldScene.PointAt(host, name == "sheet-fields-private-tooltip" ? "help-Width" : "help-EditorRows", false);
                host.SetClock(2000);
            }
            return fieldScene;
        }
        if (name.StartsWith("sheet-preferences", StringComparison.Ordinal))
        {
            var preferences = new CharacterSheetSettingsDialog(host.Api, true, 0, _ => { });
            preferences.TryOpen();
            var preferenceScene = new PreviewScene(name, preferences);
            if (name.EndsWith("dropdown", StringComparison.Ordinal))
            { host.RenderGuiDialog(preferences, 0); preferenceScene.PointAt(host, "autoopen", true); }
            return preferenceScene;
        }
        if (name.StartsWith("admin-", StringComparison.Ordinal)) return CreateAdmin(name, host);
        if (name.StartsWith("scene-marker-", StringComparison.Ordinal))
        {
            var type = typeof(SceneDescriptionData).Assembly.GetType("thebasics.ModSystems.SceneDescriptions.SceneDescriptionDialog", throwOnError: true)!;
            var data = new SceneDescriptionData { Title = "The old watchtower", Body = "Wind whistles through the broken stonework.", Color = SceneMarkerColor.Blue };
            if (name != "scene-marker-colors") data.CustomColorHex = "#A153CF";
            if (name.StartsWith("scene-marker-nearby", StringComparison.Ordinal)) data.Display = SceneDescriptionDisplay.AlwaysNearby;
            if (name == "scene-marker-nearby-on") { data.UnlimitedIconDistance = true; data.UnlimitedTextDistance = true; data.IdleBobbing = true; data.ShowBodyInBubble = true; }
            if (name == "scene-marker-custom-icon")
            {
                data.CustomColorHex = "#34B9A1";
                data.SymbolIconName = "wpStar1";
            }
            if (name == "scene-marker-custom-locked") data.LockItemCode = "ui";
            var editor = (GuiDialog)Activator.CreateInstance(type, host.Api, data, true,
                (Action<SceneDescriptionData, bool>)((_, _) => { }), (Action)(() => { }), (Action)(() => { }),
                (Action)(() => { }), (Action<SceneDescriptionData>)(_ => { }))!;
            editor.TryOpen();
            var markerScene = new PreviewScene(name, editor);
            if (name == "scene-marker-custom-picker")
            {
                host.RenderGuiDialog(editor, 0);
                markerScene.PointAt(host, "color-custom", click: true);
                var picker = (GuiElementColorPicker)editor.SingleComposer["custom-color-picker"];
                if (!picker.IsOpen || !picker.IsCustomExpanded)
                    throw new InvalidOperationException("Scene marker Custom did not open the spectrum picker.");
                host.SetMouse(0, 0);
                editor.OnMouseMove(new MouseEvent(0, 0));
            }
            return markerScene;
        }
        var languages = new List<LanguageConfigEntryMessage>
        {
            new() { OriginalName = "Common", Name = "Common", Description = "A shared language for travelers and neighbors.", Prefix = "Common", Syllables = "a,e,i,o,u,ka,ra,ta", Color = "#d4c2ff", Default = true },
            new() { OriginalName = "Old Tongue", Name = "Old Tongue", Description = "Words remembered from a distant age.", Prefix = "Old", Syllables = "al,tor,en,va", Color = "#78c6ab", Hidden = true }
        };
        var dialog = new LanguageConfigDialog(host.Api, languages,
            name == "language-error" ? "The language name is already in use. Choose a different name." : "Preview fixture: changes stay in this process.",
            name != "language-error", _ => { }, () => { }, () => { });
        var scene = new PreviewScene(name, dialog);
        dialog.TryOpen();
        host.RenderGuiDialog(dialog, 0);
        switch (name)
        {
            case "language-focus": scene.PointAt(host, "input-Name", click: true); break;
            case "language-dropdown": scene.PointAt(host, "language-selector", click: true); break;
            case "language-hover": scene.PointAt(host, "save", click: false); break;
            case "language-tooltip": scene.PointAt(host, "save", click: false); host.SetClock(2000); break;
            case "language-color-palette": scene.PointAt(host, "color-picker", click: true); break;
            case "language-color-custom":
                scene.PointAt(host, "color-picker", click: true);
                ((GuiElementColorPicker)dialog.SingleComposer["color-picker"]).ExpandCustom();
                break;
        }
        if (name.StartsWith("language-color-", StringComparison.Ordinal))
        {
            host.SetMouse(0, 0);
            dialog.OnMouseMove(new MouseEvent(0, 0));
        }
        return scene;
    }

    public void PointAt(PreviewHost host, string key, bool click)
    {
        var control = Dialog.SingleComposer[key] ?? throw new InvalidOperationException("Production control is missing: " + key);
        var bounds = control.Bounds;
        var x = (int)(bounds.absX + bounds.OuterWidth / 2);
        var y = (int)(bounds.absY + bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        Dialog.OnMouseMove(new MouseEvent(x, y));
        if (click)
        {
            Dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            Dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
    }

    private static PreviewScene CreateAdmin(string name, PreviewHost host)
    {
        // This version-checked binding executes the actual private production entry point,
        // including its dialog subclass. No copy of the layout or JSON control mapping.
        var type = typeof(ChatUiSystem);
        var fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.Name.StartsWith("_configAdmin", StringComparison.Ordinal) || f.Name.StartsWith("_configSearch", StringComparison.Ordinal) || f.Name is "_api" or "_config").ToArray();
        var previous = fields.ToDictionary(f => f, f => f.GetValue(null));
        void Set(string field, object? value) => (fields.SingleOrDefault(f => f.Name == field)
            ?? throw new MissingFieldException(type.FullName, field)).SetValue(null, value);
        void Restore() { foreach (var pair in previous) pair.Key.SetValue(null, pair.Value); }
        try
        {
            Set("_api", host.Api);
            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            if (name.StartsWith("admin-chat-colors", StringComparison.Ordinal))
            {
                config.EmoteColor = "#9EBE82";
                config.OOCColor = "#7EAFE0";
                config.GlobalOOCColor = "#E49A3B";
            }
            if (name == "admin-nametag-colors")
            {
                config.NametagBackgroundColor = "#40352980";
                config.NametagBorderColor = "#E9DDCEBF";
            }
            Set("_config", config);
            Set("_configAdminDialog", null);
            Set("_configAdminReplacing", false);
            Set("_configSearchDialog", null);
            Set("_configSearchTarget", null);
            Invoke("UpdateConfigAdminDraft", null, null, "Preview fixture: settings are not sent to a server.");
            var settingKey = name switch
            {
                "admin-bubbles" => "OverheadChatBubbleMode", "admin-discord" => "EnableTh3EssentialsDiscordRelay",
                "admin-chat-colors" or "admin-chat-colors-custom" => "EmoteColor",
                "admin-nametag-colors" => "NametagBackgroundColor", _ => "EnableGlobalOOC"
            };
            Set("_configAdminSelectedGroup", ConfigAdminSettingRegistry.Settings.Single(s => s.Key == settingKey).Group);
            Invoke("OpenConfigAdminDialog");
            var dialog = (GuiDialog?)fields.Single(f => f.Name == "_configAdminDialog").GetValue(null)
                ?? throw new InvalidOperationException("Production settings dialog did not open.");
            if (name.StartsWith("admin-search", StringComparison.Ordinal))
            {
                var draft = ConfigAdminSettingRegistry.Settings.ToDictionary(setting => setting.Key, setting => setting.GetValue(config));
                var entries = ConfigAdminSearch.Build(config, draft, draft);
                if (name == "admin-search-target")
                {
                    var target = entries.First(result => result.Key == "ProximityChatTabPosition");
                    Set("_configAdminSelectedGroup", target.Group);
                    Set("_configSearchTarget", target);
                    Invoke("OpenConfigAdminDialog");
                    dialog = (GuiDialog)fields.Single(field => field.Name == "_configAdminDialog").GetValue(null)!;
                }
                else
                {
                    var query = name == "admin-search-empty" ? "" : name == "admin-search-none" ? "missing-setting-xyz" : "proximity";
                    dialog = new ConfigSearchDialog(host.Api, entries, query, _ => { }, _ => { });
                    dialog.TryOpen();
                }
            }
            if (name is "admin-chat-colors" or "admin-chat-colors-custom" or "admin-nametag-colors")
            {
                var dialogType = dialog.GetType();
                var jump = dialogType.GetMethod("JumpTo", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(dialogType.FullName, "JumpTo");
                if (jump.Invoke(dialog, [settingKey]) is not true) throw new InvalidOperationException("Production color setting is missing: " + settingKey);
                host.RenderGuiDialog(dialog, 0);
                if (name is "admin-chat-colors-custom" or "admin-nametag-colors")
                {
                    var field = jump.DeclaringType!.GetField("_colorPickers", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(dialogType.FullName, "_colorPickers");
                    var pickers = (IDictionary<string, GuiElementColorPicker>)field.GetValue(dialog)!;
                    var picker = pickers[settingKey];
                    var bounds = picker.Bounds;
                    var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
                    var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
                    host.SetMouse(x, y);
                    dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
                    dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
                    if (!picker.IsOpen) throw new InvalidOperationException($"Color popup did not open: {settingKey}, pointer {x},{y}, bounds {bounds.absX},{bounds.absY},{bounds.OuterWidth},{bounds.OuterHeight}, clip {picker.InsideClipBounds?.absY},{picker.InsideClipBounds?.OuterHeight}");
                    if (name == "admin-chat-colors-custom") picker.ExpandCustom();
                    host.SetMouse(0, 0);
                    dialog.OnMouseMove(new MouseEvent(0, 0));
                }
            }
            return new PreviewScene(name, dialog, Restore);
        }
        catch { Restore(); throw; }
        object? Invoke(string method, params object?[] args) => (type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, method)).Invoke(null, args);
    }

    public void Dispose()
    {
        try { Dialog.Dispose(); }
        finally { restore(); }
    }
}
