using System;
using Vintagestory.API.Client;

namespace PocketDimensions;

internal sealed class PocketCreateDialog : GuiDialog
{
    private const double DialogWidth = 520;
    private readonly Action<string, string, int, int> _onSubmit;
    private readonly int _defaultSizeChunks;
    private readonly int _defaultSpawnY;
    private bool _autoSlug = true;
    private bool _updatingSlug;

    public PocketCreateDialog(ICoreClientAPI capi, int defaultSizeChunks, int defaultSpawnY, Action<string, string, int, int> onSubmit)
        : base(capi)
    {
        _defaultSizeChunks = defaultSizeChunks;
        _defaultSpawnY = defaultSpawnY;
        _onSubmit = onSubmit;
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.55;

    private void Compose()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 12;
        var rowHeight = 28;
        var fieldGap = 54;
        var y = contentTop;
        var width = DialogWidth - 20;
        var displayNameBounds = ElementBounds.Fixed(0, y + 20, width, rowHeight);
        y += fieldGap;
        var slugBounds = ElementBounds.Fixed(0, y + 20, width, rowHeight);
        y += fieldGap;
        var sizeBounds = ElementBounds.Fixed(0, y + 20, 160, rowHeight);
        var spawnBounds = ElementBounds.Fixed(180, y + 20, 160, rowHeight);
        y += fieldGap;
        var hintBounds = ElementBounds.Fixed(0, y, width, 38);
        y += 48;
        var bodyHeight = y + rowHeight + 6;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui.CreateCompo("pocket-create", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar("Create Pocket", OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddStaticText("Display name", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, contentTop, width, 20))
            .AddTextInput(displayNameBounds, OnDisplayNameChanged, CairoFont.TextInput(), "displayName")
            .AddStaticText("Slug", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, slugBounds.fixedY - 20, width, 20))
            .AddTextInput(slugBounds, OnSlugChanged, CairoFont.TextInput(), "slug")
            .AddStaticText("Size in chunks", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, sizeBounds.fixedY - 20, 160, 20))
            .AddTextInput(sizeBounds, _ => { }, CairoFont.TextInput(), "sizeChunks")
            .AddStaticText("Spawn height (Y)", CairoFont.WhiteSmallText(), ElementBounds.Fixed(180, spawnBounds.fixedY - 20, 180, 20))
            .AddTextInput(spawnBounds, _ => { }, CairoFont.TextInput(), "spawnY")
            .AddStaticText($"Blank size/spawn use defaults: {_defaultSizeChunks} chunks, spawn Y {_defaultSpawnY}. Slug controls the persistent pocket id.", CairoFont.WhiteSmallText(), hintBounds)
            .AddSmallButton("Cancel", OnCancel, ElementBounds.Fixed(0, y, 120, rowHeight))
            .AddSmallButton("Create", OnSubmit, ElementBounds.Fixed(DialogWidth - 142, y, 120, rowHeight), EnumButtonStyle.Normal, "create")
            .EndChildElements()
            .Compose(focusFirstElement: true);
        SingleComposer.GetTextInput("sizeChunks")?.SetPlaceHolderText(_defaultSizeChunks.ToString());
        SingleComposer.GetTextInput("spawnY")?.SetPlaceHolderText(_defaultSpawnY.ToString());
    }

    private void OnDisplayNameChanged(string value)
    {
        if (!_autoSlug)
        {
            return;
        }

        var slugInput = SingleComposer?.GetTextInput("slug");
        if (slugInput == null)
        {
            return;
        }

        _updatingSlug = true;
        slugInput.SetValue(PocketSlug.Normalize(value));
        _updatingSlug = false;
    }

    private void OnSlugChanged(string value)
    {
        if (!_updatingSlug)
        {
            _autoSlug = false;
        }
    }

    private bool OnSubmit()
    {
        var displayName = Text("displayName").Trim();
        var slug = Text("slug").Trim();
        if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(slug))
        {
            return true;
        }

        _onSubmit?.Invoke(displayName, slug, ParseInt("sizeChunks"), ParseInt("spawnY"));
        TryClose();
        return true;
    }

    private string Text(string key)
    {
        return SingleComposer?.GetTextInput(key)?.GetText() ?? string.Empty;
    }

    private int ParseInt(string key)
    {
        return int.TryParse(Text(key), out var value) ? value : 0;
    }

    private bool OnCancel()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }
}
