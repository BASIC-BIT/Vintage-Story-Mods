using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed record SceneEntryChoice(string Id, string Title, string AuthorName, bool? IsRead, bool IsLocked, bool CanRemove = false);

/// <summary>Selects one description from the entries attached to a single physical marker.</summary>
internal sealed class SceneEntryChooserDialog : GuiDialog
{
    internal const int MaxEntries = 8;
    private const int RowHeight = 48;
    private readonly SceneEntryChoice[] _choices;
    private readonly bool _editing;
    private readonly bool _canAdd;
    private readonly Action<string> _onSelect;
    private readonly Action _onAdd;
    private readonly Action<string> _onRemove;
    private readonly Action _onClose;
    private GuiDialogConfirm _removeConfirm;

    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    internal SceneEntryChooserDialog(ICoreClientAPI api, IReadOnlyList<SceneEntryChoice> choices, bool editing, bool canAdd,
        Action<string> onSelect, Action onAdd = null, Action<string> onRemove = null, Action onClose = null) : base(api)
    {
        _choices = choices?.ToArray() ?? throw new ArgumentNullException(nameof(choices));
        if (_choices.Length > MaxEntries) throw new ArgumentOutOfRangeException(nameof(choices), $"A scene marker supports at most {MaxEntries} entries.");
        _editing = editing;
        _canAdd = canAdd;
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
        _onAdd = onAdd;
        _onRemove = onRemove;
        _onClose = onClose;
        Compose();
    }

    private void Compose()
    {
        var top = GuiStyle.TitleBarHeight + 12;
        var footer = top + Math.Max(1, _choices.Length) * RowHeight + 8;
        var bounds = ElementBounds.Fixed(0, 0, 490, footer + 38).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-entry-chooser", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bounds)
            .AddDialogTitleBar(Lang.Get(_editing ? "thebasics:scene-entry-choose-edit" : "thebasics:scene-entry-choose-read"), () => TryClose())
            .BeginChildElements(bounds);

        AddRows(top);
        AddFooter(footer);
        SingleComposer.EndChildElements().Compose();
    }

    private void AddRows(double top)
    {
        if (_choices.Length == 0)
            SingleComposer.AddStaticText(Lang.Get("thebasics:scene-entry-empty"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(8, top + 5, 474, 25));

        for (var index = 0; index < _choices.Length; index++)
        {
            var choice = _choices[index];
            var title = string.IsNullOrWhiteSpace(choice.Title) ? Lang.Get("thebasics:scene-entry-untitled") : choice.Title;
            var y = top + index * RowHeight;
            var hasRemove = _editing && _onRemove != null && choice.CanRemove;
            SingleComposer.AddSmallButton(Shorten(title, 43), () => Select(choice.Id),
                ElementBounds.Fixed(8, y, hasRemove ? 380 : 474, 27));
            SingleComposer.AddStaticText(Shorten(ChoiceMeta(choice), 72), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(12, y + 29, 470, 18));
            if (hasRemove)
                SingleComposer.AddSmallButton(Lang.Get("thebasics:scene-entry-remove"), () => ConfirmRemove(choice.Id, title),
                    ElementBounds.Fixed(396, y, 86, 27));
        }
    }

    private void AddFooter(double footer)
    {
        if (_editing && _canAdd && _choices.Length < MaxEntries && _onAdd != null)
            SingleComposer.AddSmallButton(Lang.Get("thebasics:scene-entry-add"), Add,
                ElementBounds.Fixed(8, footer, 120, 30));
        SingleComposer.AddSmallButton(Lang.Get("Close"), () => TryClose(), ElementBounds.Fixed(362, footer, 120, 30));
    }

    private static string ChoiceMeta(SceneEntryChoice choice)
    {
        var author = string.IsNullOrWhiteSpace(choice.AuthorName) ? Lang.Get("thebasics:scene-entry-unknown-author")
            : Lang.Get("thebasics:scene-description-authored-by", choice.AuthorName);
        var status = choice.IsRead.HasValue
            ? " | " + Lang.Get(choice.IsRead.Value ? "thebasics:scene-entry-read" : "thebasics:scene-entry-unread")
            : string.Empty;
        if (choice.IsLocked) status += " | " + Lang.Get("thebasics:scene-entry-locked");
        return Shorten(author, 38) + status;
    }

    private bool Select(string id)
    {
        if (!TryClose()) return false;
        _onSelect(id);
        return true;
    }

    private bool Add()
    {
        if (!TryClose()) return false;
        _onAdd();
        return true;
    }

    private bool ConfirmRemove(string id, string title)
    {
        if (_removeConfirm?.IsOpened() == true) return false;
        _removeConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:scene-entry-remove-confirm", title), confirmed =>
        {
            _removeConfirm = null;
            if (confirmed && TryClose()) _onRemove(id);
        });
        return _removeConfirm.TryOpen();
    }

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";

    public override void OnGuiClosed()
    {
        _removeConfirm?.TryClose();
        _removeConfirm = null;
        base.OnGuiClosed();
        _onClose?.Invoke();
        Dispose();
    }
}
