using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// The journal guide: the tower spec next to a live 3D render of the two blocks it is made of and of
/// the cabin entity itself, all turning. Opened with sneak + right-click on a pylon head.
///
/// The cabin is an EntityRopewayCabin that is never spawned into the world, so nothing creates its
/// renderer for us - we build the EntityShapeRenderer by hand, the way ClientSystemEntities does on
/// spawn (ClientSystemEntities.cs:145), and dispose it with the dialog.
/// </summary>
public sealed class RopewayGuideDialog : GuiDialog
{
    private const double DialogWidth = 560;
    private const double ContentWidth = DialogWidth - 48;
    private const double ViewportHeight = 190;

    private ElementBounds viewportBounds;
    private ItemSlot pylonSlot;
    private ItemSlot braceSlot;

    private Entity cabin;
    private EntityShapeRenderer cabinRenderer;
    private float yaw;

    public RopewayGuideDialog(ICoreClientAPI capi) : base(capi)
    {
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.28;

    public void Show()
    {
        if (SingleComposer == null) Compose();
        TryOpen();
    }

    private void Compose()
    {
        var contentTop = GuiStyle.TitleBarHeight + 10;
        viewportBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, ViewportHeight);
        var textBounds = ElementBounds.Fixed(0, contentTop + ViewportHeight + 12, ContentWidth, 285);
        var buttonY = textBounds.fixedY + textBounds.fixedHeight + 8;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, buttonY + 36)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui.CreateCompo("ropeway-guide", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("ropeway:dlg-guide-title"), () => TryClose())
            .BeginChildElements(bodyBounds)
            // The inset takes viewportBounds itself, not a copy: OnRenderGUI needs its renderX/renderY, and
            // only bounds that are actually in the composer tree get CalcWorldBounds called on them.
            .AddInset(viewportBounds, 3)
            .AddRichtext(Lang.Get("ropeway:dlg-guide-body"), CairoFont.WhiteSmallText(), textBounds)
            .AddSmallButton(Lang.Get("Close"), OnClose, ElementBounds.Fixed(ContentWidth - 120, buttonY, 110, 30))
            .EndChildElements()
            .Compose(focusFirstElement: false);
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();

        pylonSlot ??= SlotFor("pylonhead-north");
        braceSlot ??= SlotFor("brace-north");
        EnsureCabin();
    }

    private ItemSlot SlotFor(string blockCode)
    {
        var block = capi.World.GetBlock(new AssetLocation("ropeway", blockCode));
        return block == null ? null : new DummySlot(new ItemStack(block));
    }

    /// <summary>
    /// A world-less copy of the cabin, purely to have something for RenderEntityToGui to draw. Behaviors
    /// are stripped: selectionboxes, interpolateposition and seatable all register renderers or assume a
    /// position in a loaded chunk, and none of them contribute anything to a still 3D portrait.
    /// </summary>
    private void EnsureCabin()
    {
        if (cabin != null) return;

        try
        {
            var type = capi.World.GetEntityType(new AssetLocation("ropeway", RopewayLinkService.CabinEntityCode));
            if (type == null) return;

            var properties = type.Clone();
            properties.Client.BehaviorsAsJsonObj = null;

            var entity = capi.ClassRegistry.CreateEntity(properties);
            if (entity == null) return;

            entity.Code = properties.Code;
            entity.Initialize(properties, capi, 0);

            cabinRenderer = new EntityShapeRenderer(entity, capi);
            entity.Properties.Client.Renderer = cabinRenderer;

            // TesselateShape() no-ops until the renderer considers itself loaded, and RenderToGui is the
            // only thing that will ever ask it to tesselate.
            cabinRenderer.OnEntityLoaded();
            cabin = entity;
        }
        catch (Exception e)
        {
            // A cosmetic guide page must never take the client down with it.
            capi.Logger.Warning("Ropeway: could not build the guide cabin preview: {0}", e);
            DisposeCabin();
        }
    }

    public override void OnRenderGUI(float dt)
    {
        base.OnRenderGUI(dt);

        if (viewportBounds == null) return;

        yaw += dt * 0.6f;

        var cell = viewportBounds.InnerWidth / 3;
        var y = viewportBounds.renderY + viewportBounds.InnerHeight / 2;
        var size = (float)GuiElement.scaled(70);

        capi.Render.GlPushMatrix();
        capi.Render.GlRotate(-14f, 1f, 0f, 0f);

        RenderStack(dt, pylonSlot, viewportBounds.renderX + cell * 0.5, y, size);
        RenderStack(dt, braceSlot, viewportBounds.renderX + cell * 1.5, y, size);
        RenderCabin(dt, viewportBounds.renderX + cell * 2.5, y);

        capi.Render.GlPopMatrix();
    }

    private void RenderStack(float dt, ItemSlot slot, double x, double y, float size)
    {
        if (slot?.Itemstack == null) return;
        capi.Render.RenderItemstackToGui(slot, x, y, 250.0, size, -1, dt, shading: true, rotate: true, showStackSize: false);
    }

    private void RenderCabin(float dt, double x, double y)
    {
        if (cabin?.Properties?.Client?.Renderer == null) return;

        try
        {
            // ponytail: size and the downward nudge are eyeballed against a 3x4x2.5 cabin whose shape
            // origin sits mid-body. They are the calibration knob if the cabin sits high or low in the box.
            capi.Render.RenderEntityToGui(dt, cabin, x, y + GuiElement.scaled(30), 250.0, yaw, (float)GuiElement.scaled(34), -1);
        }
        catch (Exception e)
        {
            capi.Logger.Warning("Ropeway: guide cabin preview render failed, dropping it: {0}", e);
            DisposeCabin();
        }
    }

    private bool OnClose()
    {
        TryClose();
        return true;
    }

    private void DisposeCabin()
    {
        cabinRenderer?.Dispose();
        cabinRenderer = null;
        cabin = null;
    }

    public override void Dispose()
    {
        DisposeCabin();
        base.Dispose();
    }
}
