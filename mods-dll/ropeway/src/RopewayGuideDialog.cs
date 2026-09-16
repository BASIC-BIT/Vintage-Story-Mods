using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// The journal guide: the tower spec next to a live 3D render of the three blocks it is made of and of
/// the cabin entity itself, all turning. Opened with sneak + right-click on a pylon footing.
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

    /// <summary>
    /// The blocks the strip turns, left to right, then the cabin. All three FOOTINGS are here because the
    /// guide is the mod's primary build-teaching surface and which footing you place is now the whole of
    /// what makes a tower a drive or a tensioner - a player who never sees the station footings can build
    /// every tower on a line and never learn what makes one move. The machine legs' seven blocks are named
    /// in the guide text instead: seven more portraits would shrink the row to nothing.
    /// </summary>
    private static readonly string[] StripBlocks =
    {
        "pylonbase-north", "drivestation-north", "tensionstation-north", "pylonhead-north", "brace-north",
        "bullwheel-north"
    };

    private readonly ItemSlot[] slots = new ItemSlot[StripBlocks.Length];

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
        // Recomposed every time, not cached: the body names the player's live hotkey bindings, and those can
        // change in Settings > Controls between two openings.
        SingleComposer?.Dispose();
        Compose();
        TryOpen();
    }

    /// <summary>
    /// Two passes, and the first one exists only to measure. <c>GuiComposer.Compose</c> calls
    /// <c>BeforeCalcBounds</c> on every element before it sizes anything, and
    /// <c>GuiElementRichtext.BeforeCalcBounds</c> -&gt; <c>CalcHeightAndPositions</c> overwrites its
    /// <c>Bounds.fixedHeight</c> with the height the body actually laid out to - so after one compose the
    /// text has told us how tall it is. Everything else on this dialog is read off that number BEFORE
    /// compose: the Close button's Y, the shaded background, the dialog's own size. That is why the height
    /// used to be a hardcoded 500 with a comment asking the next person to raise it, and why it went stale
    /// the first time a paragraph was added. Asking costs one throwaway compose on a right-click, and the
    /// throwaway frees itself: <c>IGuiAPI.CreateCompo</c> disposes whatever is already cached under the same
    /// dialog name.
    /// </summary>
    private void Compose()
    {
        var measured = Compose(0);
        Compose(measured);
    }

    /// <summary>
    /// Builds the dialog with <paramref name="textHeight"/> allotted to the body, and returns the height the
    /// body really needed.
    /// </summary>
    private double Compose(double textHeight)
    {
        var contentTop = GuiStyle.TitleBarHeight + 10;
        viewportBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, ViewportHeight);
        var textBounds = ElementBounds.Fixed(0, contentTop + ViewportHeight + 12, ContentWidth, textHeight);
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
            // The two riding keys are substituted from the player's CURRENT bindings, not written into the
            // string - a guide that names a key the player has rebound is worse than one that names none.
            .AddRichtext(
                Lang.Get("ropeway:dlg-guide-body",
                    EntityRopewayCabin.Binding(capi, RopewayModSystem.StopHotkey, "ropeway:hotkey-stop"),
                    EntityRopewayCabin.Binding(capi, RopewayRideCamera.Hotkey, "ropeway:hotkey-ridecam")),
                CairoFont.WhiteSmallText(), textBounds)
            .AddSmallButton(Lang.Get("Close"), OnClose, ElementBounds.Fixed(ContentWidth - 120, buttonY, 110, 30))
            .EndChildElements()
            .Compose(focusFirstElement: false);

        return textBounds.fixedHeight;
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();

        for (var i = 0; i < StripBlocks.Length; i++) slots[i] ??= SlotFor(StripBlocks[i]);
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

        // The three footings and the three crossarm blocks, then the cabin. The posts are player-chosen wood
        // and have no one block to show, and the machine legs are named in the text rather than turned here.
        // It used to say "the drive pair", from when the strip ended with the housing and the weight; both
        // are cells of a station now and neither is in StripBlocks.
        var cells = StripBlocks.Length + 1;
        var cell = viewportBounds.InnerWidth / cells;
        var y = viewportBounds.renderY + viewportBounds.InnerHeight / 2;

        // Shrinks as the strip grows, so adding a block narrows the row rather than overflowing it.
        var size = (float)GuiElement.scaled(360.0 / cells);

        capi.Render.GlPushMatrix();
        capi.Render.GlRotate(-14f, 1f, 0f, 0f);

        for (var i = 0; i < slots.Length; i++)
        {
            RenderStack(dt, slots[i], viewportBounds.renderX + cell * (i + 0.5), y, size);
        }

        RenderCabin(dt, viewportBounds.renderX + cell * (cells - 0.5), y);

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
            capi.Render.RenderEntityToGui(dt, cabin, x, y + GuiElement.scaled(30), 250.0, yaw, (float)GuiElement.scaled(28), -1);
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
