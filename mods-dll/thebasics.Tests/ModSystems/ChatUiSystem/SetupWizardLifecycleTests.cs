using System.Reflection;
using Cairo;
using NSubstitute;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardLifecycleTests
{
    [VisualTheory]
    [InlineData(1.0)]
    public void FailedConstructorDisposesItsUnfinishedComposerAndPreservesOriginalException(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.Returns(host.Api.Render);
        api.Settings.Returns(host.Api.Settings);
        var failure = new InvalidOperationException("composition failed");
        var element = new FailingComposeElement(host.Api, failure);
        api.Gui.Returns(StrictApiProxy.Create<IGuiAPI>((method, arguments) =>
        {
            if (method.Name != "CreateCompo") return method.Invoke(host.Api.Gui, arguments);
            var composer = host.Api.Gui.CreateCompo((string)arguments[0]!, (ElementBounds)arguments[1]!);
            return composer.AddStaticElement(element, "composition-failure");
        }));

        var actual = Assert.Throws<InvalidOperationException>(() => new SetupWizardDialog(api,
            new(SetupWizardCaptureScenes.DefaultValues()), "run", true, null!, null!, null!, layoutOnly: true));

        Assert.Same(failure, actual);
        Assert.True(element.WasDisposed);
    }

    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedTransientDialogUnregistersAndSuspendedWizardCanReopen(bool invitation)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        using GuiDialog dialog = invitation
            ? new SetupWizardInvitationDialog(host.Api, null!, null!)
            : new SetupWizardDialog(host.Api, new(SetupWizardCaptureScenes.DefaultValues()), "run", true,
                null!, null!, null!, layoutOnly: true);
        dialog.TryOpen();
        Assert.Contains(dialog, host.Api.Gui.LoadedGuis);

        if (dialog is SetupWizardDialog wizard) wizard.Suspend();
        else dialog.TryClose();

        Assert.DoesNotContain(dialog, host.Api.Gui.LoadedGuis);
        Assert.DoesNotContain(dialog, host.Api.Gui.OpenedGuis);
        dialog.TryOpen();
        Assert.Contains(dialog, host.Api.Gui.LoadedGuis);
        Assert.Contains(dialog, host.Api.Gui.OpenedGuis);
    }

    [VisualTheory]
    [InlineData(1.0)]
    public void CancelRetainsOwnedConfirmationAndDisposalClosesTheCurrentConfirmation(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        using var wizard = new SetupWizardDialog(host.Api, draft, "run", true,
            null!, null!, null!, layoutOnly: true);
        wizard.TryOpen();
        var confirmField = typeof(SetupWizardDialog).GetField("_closeConfirm", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.False(wizard.TryClose());
        var first = Assert.IsAssignableFrom<GuiDialogConfirm>(confirmField.GetValue(wizard));
        ClickButton(host, first, "Cancel");

        Assert.True(wizard.IsOpened());
        Assert.True(draft.IsDirty);
        Assert.Same(first, confirmField.GetValue(wizard));
        Assert.DoesNotContain(first, host.Api.Gui.LoadedGuis);
        Assert.DoesNotContain(first, host.Api.Gui.OpenedGuis);

        Assert.False(wizard.TryClose());
        var second = Assert.IsAssignableFrom<GuiDialogConfirm>(confirmField.GetValue(wizard));
        Assert.NotSame(first, second);
        Assert.Contains(second, host.Api.Gui.OpenedGuis);

        wizard.Dispose();
        Assert.False(second.IsOpened());
        Assert.Empty(host.Api.Gui.LoadedGuis);
        Assert.Empty(host.Api.Gui.OpenedGuis);
    }

    private static void ClickButton(PreviewHost host, GuiDialog dialog, string text)
    {
        var elements = (Dictionary<string, GuiElement>)typeof(GuiComposer)
            .GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog.SingleComposer)!;
        var bounds = elements.Values.OfType<GuiElementTextButton>().Single(button => button.Text == text).Bounds;
        var x = (int)(bounds.absX + bounds.OuterWidth / 2);
        var y = (int)(bounds.absY + bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        dialog.OnMouseMove(new MouseEvent(x, y));
        dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }

    private sealed class FailingComposeElement(ICoreClientAPI api, Exception failure)
        : GuiElement(api, ElementBounds.Fixed(0, 0, 10, 10))
    {
        public bool WasDisposed { get; private set; }
        public override void ComposeElements(Context context, ImageSurface surface) => throw failure;
        public override void Dispose()
        {
            WasDisposed = true;
            base.Dispose();
        }
    }
}
