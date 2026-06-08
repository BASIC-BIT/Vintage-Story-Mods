using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PocketDimensions;

internal sealed class PocketCoordinatesHud : HudElement
{
    private PocketHudStateMessage _state = new PocketHudStateMessage();
    private readonly long _tickListenerId;

    public PocketCoordinatesHud(ICoreClientAPI capi)
        : base(capi)
    {
        Compose();
        _tickListenerId = capi.Event.RegisterGameTickListener(OnTick, 250);
    }

    public override string ToggleKeyCombinationCode => null;

    public void SetState(PocketHudStateMessage state)
    {
        _state = state ?? new PocketHudStateMessage();
        UpdateText();
    }

    public override void Dispose()
    {
        capi.Event.UnregisterGameTickListener(_tickListenerId);
        base.Dispose();
    }

    private void Compose()
    {
        var textBounds = ElementBounds.Fixed(EnumDialogArea.None, 0, 0, 230, 56);
        var bgBounds = textBounds.ForkBoundingParent(5, 5, 5, 5);
        var bounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightTop).WithFixedAlignmentOffset(0 - GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding + 70);
        SingleComposer = capi.Gui.CreateCompo("pocketcoordinateshud", bounds)
            .AddGameOverlay(bgBounds)
            .AddDynamicText("", CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center), textBounds, "text")
            .Compose();
    }

    private void OnTick(float dt)
    {
        if (_state.InPocket)
        {
            if (!IsOpened())
            {
                TryOpen();
            }

            UpdateText();
        }
        else if (IsOpened())
        {
            TryClose();
        }
    }

    private void UpdateText()
    {
        if (SingleComposer == null)
        {
            return;
        }

        var text = _state.InPocket
            ? $"{_state.PocketName}  Layer {FormatLayer(_state.LayerIndex)}\n{_state.LocalX}, {_state.LocalY}, {_state.LocalZ}"
            : string.Empty;
        SingleComposer.GetDynamicText("text").SetNewText(text);
    }

    private static string FormatLayer(int index)
    {
        return index > 0 ? "+" + index : index.ToString();
    }
}
