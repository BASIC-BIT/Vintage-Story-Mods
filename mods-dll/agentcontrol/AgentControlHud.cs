using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace AgentControl;

internal sealed class AgentControlHud : HudElement
{
    private string _text = string.Empty;

    public AgentControlHud(ICoreClientAPI api) : base(api)
    {
        var textBounds = ElementBounds.Fixed(EnumDialogArea.None, 0, 0, 230, 84);
        var backgroundBounds = textBounds.ForkBoundingParent(5, 5, 5, 5);
        var bounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding + 70);
        SingleComposer = api.Gui.CreateCompo("agentcontrolhud", bounds)
            .AddGameOverlay(backgroundBounds)
            .AddDynamicText("", CairoFont.WhiteSmallishText(), textBounds, "text")
            .Compose();
    }

    public override string? ToggleKeyCombinationCode => null;

    public void SetState(bool enabled, bool active, bool mutationGranted)
    {
        if (!enabled)
        {
            if (IsOpened())
            {
                TryClose();
            }
            return;
        }

        var text = $"AGENT CONTROL: {(active ? "ACTIVE" : "READY")}\nMUTATION: {(mutationGranted ? "GRANTED" : "DENIED")}\nKill: Ctrl+Alt+F9";
        if (text != _text)
        {
            _text = text;
            SingleComposer.GetDynamicText("text").SetNewText(_text);
        }
        if (!IsOpened())
        {
            TryOpen();
        }
    }
}
