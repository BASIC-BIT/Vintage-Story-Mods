using System;
using thebasics.Configs;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.Analytics;

public class AnalyticsConsentDialog : GuiDialog
{
    private const double DialogWidth = 660;
    private const double ContentWidth = DialogWidth - 48;
    private readonly AnalyticsConsentPromptMessage _message;
    private readonly Action<string> _onChoice;

    public AnalyticsConsentDialog(ICoreClientAPI capi, AnalyticsConsentPromptMessage message, Action<string> onChoice)
        : base(capi)
    {
        _message = message ?? new AnalyticsConsentPromptMessage();
        _onChoice = onChoice;
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override string ToggleKeyCombinationCode => null;
    public override double DrawOrder => 0.35;

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 12;
        var headingHeight = 20;
        var sectionGap = 10;
        var whyHeadingBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, headingHeight);
        var whyBodyBounds = ElementBounds.Fixed(0, whyHeadingBounds.fixedY + headingHeight + 2, ContentWidth, 42);
        var choicesHeadingBounds = ElementBounds.Fixed(0, whyBodyBounds.fixedY + whyBodyBounds.fixedHeight + sectionGap, ContentWidth, headingHeight);
        var choicesBodyBounds = ElementBounds.Fixed(0, choicesHeadingBounds.fixedY + headingHeight + 2, ContentWidth, 62);
        var statusHeadingBounds = ElementBounds.Fixed(0, choicesBodyBounds.fixedY + choicesBodyBounds.fixedHeight + sectionGap, ContentWidth, headingHeight);
        var statusBounds = ElementBounds.Fixed(0, statusHeadingBounds.fixedY + headingHeight + 2, ContentWidth, 24);
        var buttonY = statusBounds.fixedY + statusBounds.fixedHeight + 14;
        var buttonHeight = 30;
        var buttonGap = 12;
        var buttonWidth = (ContentWidth - buttonGap) / 2;
        var footerBounds = ElementBounds.Fixed(0, buttonY + (buttonHeight * 2) + 18, ContentWidth, 28);
        var bodyHeight = footerBounds.fixedY + footerBounds.fixedHeight + 10;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("thebasics-analytics-consent", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:analytics-ui-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds);

        AddHeading(composer, Lang.Get("thebasics:analytics-ui-why-heading"), whyHeadingBounds);
        AddRichtext(composer, Lang.Get("thebasics:analytics-consent-prompt-intro"), whyBodyBounds);
        AddHeading(composer, Lang.Get("thebasics:analytics-ui-choices-heading"), choicesHeadingBounds);
        AddRichtext(composer, Lang.Get("thebasics:analytics-ui-choices-body"), choicesBodyBounds);
        AddHeading(composer, Lang.Get("thebasics:analytics-ui-status-heading"), statusHeadingBounds);
        AddRichtext(composer, Lang.Get("thebasics:analytics-ui-status", _message.CurrentConsentLevel ?? AnalyticsConsentLevels.Unknown, _message.ConsentVersion), statusBounds);

        composer
            .AddSmallButton(Lang.Get("thebasics:analytics-ui-server-button"), () => Choose(AnalyticsConsentLevels.Server), ElementBounds.Fixed(0, buttonY, buttonWidth, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:analytics-ui-personalized-button"), () => Choose(AnalyticsConsentLevels.Personalized), ElementBounds.Fixed(buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:analytics-ui-off-button"), () => Choose(AnalyticsConsentLevels.Disabled), ElementBounds.Fixed(0, buttonY + buttonHeight + 8, buttonWidth, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:analytics-ui-later-button"), OnMaybeLater, ElementBounds.Fixed(buttonWidth + buttonGap, buttonY + buttonHeight + 8, buttonWidth, buttonHeight));

        AddRichtext(composer, Lang.Get("thebasics:analytics-ui-command-fallback", _message.CommandName ?? "basicsanalytics"), footerBounds);

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
    }

    private bool Choose(string consentLevel)
    {
        _onChoice?.Invoke(consentLevel);
        TryClose();
        return true;
    }

    private bool OnMaybeLater()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }

    private void AddRichtext(GuiComposer composer, string text, ElementBounds bounds)
    {
        composer.AddRichtext(VtmlUtil.Richtextify(capi, VtmlUtils.EscapeVtml(text), CairoFont.WhiteSmallText()), bounds);
    }

    private void AddHeading(GuiComposer composer, string text, ElementBounds bounds)
    {
        composer.AddStaticText(text, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), bounds);
    }
}
