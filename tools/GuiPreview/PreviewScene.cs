using System.Reflection;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
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

    public static string[] Names => ["language-default", "language-focus", "language-dropdown", "language-hover", "language-tooltip", "language-error", "admin-chat", "admin-bubbles", "admin-discord"];

    public static PreviewScene Create(string name, PreviewHost host)
    {
        if (!Names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown GUI fixture: " + name);
        if (name.StartsWith("admin-", StringComparison.Ordinal)) return CreateAdmin(name, host);
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
            case "language-dropdown": scene.PointAt(host, "dropdown-Default", click: true); break;
            case "language-hover": scene.PointAt(host, "save", click: false); break;
            case "language-tooltip": scene.PointAt(host, "save", click: false); host.SetClock(2000); break;
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
            .Where(f => f.Name.StartsWith("_configAdmin", StringComparison.Ordinal) || f.Name is "_api" or "_config").ToArray();
        var previous = fields.ToDictionary(f => f, f => f.GetValue(null));
        void Set(string field, object? value) => (fields.SingleOrDefault(f => f.Name == field)
            ?? throw new MissingFieldException(type.FullName, field)).SetValue(null, value);
        void Restore() { foreach (var pair in previous) pair.Key.SetValue(null, pair.Value); }
        try
        {
            Set("_api", host.Api);
            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            Set("_config", config);
            Set("_configAdminDialog", null);
            Invoke("UpdateConfigAdminDraft", null, null, "Preview fixture: settings are not sent to a server.");
            var settingKey = name switch { "admin-bubbles" => "OverheadChatBubbleMode", "admin-discord" => "EnableTh3EssentialsDiscordRelay", _ => "EnableGlobalOOC" };
            Set("_configAdminSelectedGroup", ConfigAdminSettingRegistry.Settings.Single(s => s.Key == settingKey).Group);
            Invoke("OpenConfigAdminDialog");
            var dialog = (GuiDialog?)fields.Single(f => f.Name == "_configAdminDialog").GetValue(null)
                ?? throw new InvalidOperationException("Production settings dialog did not open.");
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
