using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
namespace thebasics.ModSystems.ChatUiSystem;

// Native text inputs replace GL scissor state, and dropdowns render after EndClip by draw order.
internal static class SetupWizardViewportClip
{
    internal static void Render(ICoreClientAPI api, ElementBounds clip, Action render)
    {
        if (clip == null) { render(); return; }
        api.Render.PushScissor(clip, stacking: true);
        try { render(); } finally { api.Render.PopScissor(); }
    }
    internal static bool FullyInside(ElementBounds bounds, ElementBounds clip) => clip == null ||
        bounds.renderY >= clip.renderY - .1 && bounds.renderY + bounds.OuterHeight <= clip.renderY + clip.InnerHeight + .1;
}

internal sealed class SetupWizardViewportTextInput(ICoreClientAPI api, ElementBounds bounds, Action<string> changed, CairoFont font)
    : GuiElementTextInput(api, bounds, changed, font)
{
    private LoadedTexture background = new(api);
    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        using var image = new ImageSurface(Format.Argb32, Math.Max(1,Bounds.OuterWidthInt), Math.Max(1,Bounds.OuterHeightInt));
        using var local = genContext(image);
        local.Translate(-Bounds.drawX,-Bounds.drawY);
        base.ComposeElements(local,image);
        generateTexture(image,ref background);
    }
    public override void RenderInteractiveElements(float dt) => SetupWizardViewportClip.Render(api,InsideClipBounds,()=>
    {
        api.Render.Render2DTexturePremultipliedAlpha(background.TextureId,Bounds);
        // Match the production sheet: native single-line text renders only when its editor fits the clip.
        if (SetupWizardViewportClip.FullyInside(Bounds,InsideClipBounds)) base.RenderInteractiveElements(dt);
    });
    public override void Dispose() { background.Dispose(); base.Dispose(); }
}

internal sealed class SetupWizardViewportDropDown(ICoreClientAPI api,string[] values,string[] names,int index,
    SelectionChangedDelegate changed,ElementBounds bounds,CairoFont font) : GuiElementDropDown(api,values,names,index,changed,bounds,font,false)
{
    private LoadedTexture background = new(api);
    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        using var image = new ImageSurface(Format.Argb32,Math.Max(1,Bounds.OuterWidthInt),Math.Max(1,Bounds.OuterHeightInt));
        using var local=genContext(image); local.Translate(-Bounds.drawX,-Bounds.drawY);
        base.ComposeElements(local,image); generateTexture(image,ref background);
    }
    private double menuY = double.NaN;
    private void BoundMenu()
    {
        if (InsideClipBounds == null) return;
        var height=Math.Max(1,(int)((InsideClipBounds.renderY+InsideClipBounds.InnerHeight-Bounds.renderY-Bounds.OuterHeight)/RuntimeEnv.GUIScale));
        var changed=listMenu.MaxHeight!=height||menuY!=Bounds.renderY;
        listMenu.MaxHeight=height; menuY=Bounds.renderY;
        if(changed&&listMenu.IsOpened) listMenu.Open();
    }
    public override bool IsPositionInside(int x,int y) => (InsideClipBounds==null||InsideClipBounds.PointInside(x,y)) && base.IsPositionInside(x,y);
    public override void OnMouseDown(ICoreClientAPI api,MouseEvent args)
    {
        if(InsideClipBounds!=null&&!InsideClipBounds.PointInside(args.X,args.Y)) return;
        BoundMenu(); base.OnMouseDown(api,args);
    }
    public override void RenderInteractiveElements(float dt) => SetupWizardViewportClip.Render(api,InsideClipBounds,()=>
    { BoundMenu(); api.Render.Render2DTexturePremultipliedAlpha(background.TextureId,Bounds); base.RenderInteractiveElements(dt); });
    public override void Dispose() { background.Dispose(); base.Dispose(); }
}

internal sealed class SetupWizardViewportButton(ICoreClientAPI api,string text,ActionConsumable clicked,ElementBounds bounds)
    : GuiElementTextButton(api,text,CairoFont.SmallButtonText(EnumButtonStyle.Normal),
        CairoFont.SmallButtonText(EnumButtonStyle.Normal).WithColor((double[])GuiStyle.ActiveButtonTextColor.Clone()),clicked,bounds)
{
    public override bool IsPositionInside(int x, int y) => (InsideClipBounds == null || InsideClipBounds.PointInside(x, y)) && base.IsPositionInside(x, y);
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (InsideClipBounds != null && !InsideClipBounds.PointInside(args.X, args.Y)) return;
        base.OnMouseDown(api, args);
    }
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (InsideClipBounds != null && !InsideClipBounds.PointInside(args.X, args.Y)) return;
        base.OnMouseUp(api, args);
    }
    public override void RenderInteractiveElements(float dt) => SetupWizardViewportClip.Render(api,InsideClipBounds,()=>base.RenderInteractiveElements(dt));
}

internal sealed class SetupWizardViewportRichtext(ICoreClientAPI api,string text,CairoFont font,ElementBounds bounds)
    : GuiElementRichtext(api,VtmlUtil.Richtextify(api,text,font),bounds)
{
    public override void RenderInteractiveElements(float dt) => SetupWizardViewportClip.Render(api,InsideClipBounds,()=>base.RenderInteractiveElements(dt));
}
