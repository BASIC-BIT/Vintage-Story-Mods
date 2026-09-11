using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Utilities;
using Vintagestory.API.Config;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneMarkerIconRenderer : IRenderer
{
    private readonly ICoreClientAPI _api;
    private readonly HashSet<SceneDescriptionBlockEntity> _markers = new();
    // ponytail: grows once per icon and colour actually used and is only freed on dispose. Bounded by the
    // catalog in practice; evict least-recently-used entries if a world ever puts thousands in view.
    private readonly Dictionary<(string Icon, SceneMarkerColor Color), LoadedTexture> _icons = new();
    private readonly List<(SceneDescriptionBlockEntity Marker, Vec3d Position, float Opacity, float TextOpacity, float Focus, double Depth, bool Read)> _visible = new();
    private readonly Dictionary<SceneDescriptionBlockEntity, ((string Title, string Body, bool ShowBody, string Icon) Content, float GuiScale, LoadedTexture Texture)> _descriptions = new();
    private readonly HashSet<SceneDescriptionBlockEntity> _shownDescriptions = new();
    private readonly Dictionary<SceneDescriptionBlockEntity, float> _focus = new();
    private readonly Matrixf _model = new();
    private MeshRef _quad;

    internal SceneMarkerIconRenderer(ICoreClientAPI api)
    {
        _api = api;
        OrthoPass = new BubblePass(this);
    }

    public double RenderOrder => 1.11; // After terrain and vanilla sign rendering.
    public int RenderRange => int.MaxValue;

    /// <summary>Register this alongside the icon renderer on <see cref="EnumRenderStage.Ortho"/>.</summary>
    internal IRenderer OrthoPass { get; }

    internal void Register(SceneDescriptionBlockEntity marker) => _markers.Add(marker);
    internal void Unregister(SceneDescriptionBlockEntity marker)
    {
        _markers.Remove(marker);
        _focus.Remove(marker);
        RemoveDescription(marker);
    }

    private void RemoveDescription(SceneDescriptionBlockEntity marker)
    {
        if (_descriptions.Remove(marker, out var entry)) entry.Texture?.Dispose();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Opaque) return;
        var player = _api.World?.Player?.Entity;
        // Gated here rather than at registration: the synced server config arrives after the chunks
        // around the player, so there is nothing to read yet when this renderer is registered.
        if (player == null || _markers.Count == 0 || !SceneDescriptionSystem.SceneMarkersEnabled(_api))
        {
            _visible.Clear(); // The Ortho pass reads this list later in the same frame.
            return;
        }
        var render = _api.Render;
        _shownDescriptions.Clear();
        GatherVisibleIcons(deltaTime);
        if (_visible.Count == 0)
        {
            foreach (var marker in _descriptions.Keys.Where(marker => !_shownDescriptions.Contains(marker)).ToArray()) RemoveDescription(marker);
            return;
        }
        // Transparent symbols must blend back-to-front because they do not write terrain depth.
        _visible.Sort((left, right) => right.Depth.CompareTo(left.Depth));
        var previousShader = render.CurrentActiveShader;
        previousShader?.Stop();
        try
        {
            render.GLEnableDepthTest();
            render.GLDepthMask(false);
            render.GlDisableCullFace();
            render.GlToggleBlend(true, EnumBlendMode.Standard);
            foreach (var icon in _visible)
            {
                if (icon.Opacity <= 0) continue;
                var size = SceneDescriptionData.IndicatorBlocks * icon.Marker.Data.IndicatorScale * (1 + 0.10f * icon.Focus);
                // A marker this player has read settles down: half the bob height at half the speed.
                var (amplitude, period) = icon.Read ? (0.025, 8000.0) : (0.05, 4000.0);
                var bob = icon.Marker.Data.IdleBobbing ? amplitude * Math.Sin(_api.World.ElapsedMilliseconds * (Math.PI * 2 / period)) : 0;
                RenderQuad(icon.Marker, GetIcon(icon.Marker.Data), icon.Position.AddCopy(0, bob, 0), icon.Opacity * SceneMarkerVisuals.IndicatorOpacity, size, size);
            }
            // Safe to evict here even though the bubbles draw later in Ortho: GatherVisibleIcons
            // marks every marker whose text opacity is above zero, which is the Ortho pass's gate.
            foreach (var marker in _descriptions.Keys.ToArray())
                if (!_shownDescriptions.Contains(marker)) RemoveDescription(marker);
        }
        finally
        {
            render.CurrentActiveShader?.Stop();
            render.GLDepthMask(true);
            render.GlEnableCullFace();
            render.GlToggleBlend(true);
            previousShader?.Use();
        }
    }

    private void GatherVisibleIcons(float deltaTime)
    {
        var player = _api.World.Player.Entity;
        var render = _api.Render;
        _visible.Clear();
        foreach (var marker in _markers)
        {
            if (marker.Pos.dimension != player.Pos.Dimension) continue;
            var position = marker.Pos.ToVec3d().Add(0.5, 0.65, 0.5);
            var distance = player.Pos.XYZ.DistanceTo(position);
            // A marker this player has read keeps a dimmed icon and never shows a bubble.
            var read = SceneReadMarks.IsRead(marker.Pos, marker.Data.ReadStamp);
            var opacity = marker.Data.GetIconOpacity(distance) * (read ? 0.5f : 1f);
            position.Y += marker.Data.HeightOffset;
            var selected = _api.World.Player.CurrentBlockSelection?.Position?.Equals(marker.Pos) == true;
            var targeted = selected && marker.Data.Display == SceneDescriptionDisplay.WhenTargeted && marker.Data.ShouldShowDescription(true);
            var textOpacity = marker.Data.ShouldShowDescription(selected) ? marker.Data.GetTextOpacity(distance) : 0;
            if (targeted) textOpacity = 1;
            if (read) textOpacity = 0;
            var focus = _focus.GetValueOrDefault(marker);
            focus += ((selected ? 1 : 0) - focus) * (1 - MathF.Exp(-10 * Math.Max(0, deltaTime)));
            _focus[marker] = focus;
            var radius = 2.0;
            if (textOpacity > 0)
            {
                var text = GetDescription(marker);
                if (text != null)
                {
                    var (width, height) = SceneBubbleLayout.Size(text.Width, text.Height, RuntimeEnv.GUIScale, marker.Data.BubbleRenderScale);
                    radius = SceneBubbleLayout.CullRadius(width, height, marker.Data.IndicatorScale);
                }
            }
            if ((opacity <= 0 && textOpacity <= 0) || !render.DefaultFrustumCuller.SphereInFrustum(position.X, position.Y, position.Z, radius)) continue;
            var camera = player.CameraPos;
            var view = render.CameraMatrixOriginf;
            var depth = -(view[2] * (position.X - camera.X) + view[6] * (position.Y - camera.Y) + view[10] * (position.Z - camera.Z));
            _visible.Add((marker, position, opacity, textOpacity, focus, depth, read));
        }
    }

    /// <summary>
    /// Bubbles draw flat on the screen, not as world-space quads: a quad large enough to hold the
    /// text intersects the wall behind its marker and gets clipped diagonally. The depth test that
    /// hid a bubble behind terrain is replaced by an explicit line-of-sight check, so it vanishes
    /// rather than clipping.
    /// </summary>
    private void RenderBubbles()
    {
        // Non-empty only if the Opaque pass ran this frame, which is also what proves the player exists.
        if (_visible.Count == 0) return;
        var player = _api.World.Player;
        foreach (var icon in _visible)
            if (icon.TextOpacity > 0) RenderBubble(player, icon.Marker, icon.Position, icon.TextOpacity);
    }

    private void RenderBubble(IClientPlayer player, SceneDescriptionBlockEntity marker, Vec3d position, float textOpacity)
    {
        var targeted = player.CurrentBlockSelection?.Position?.Equals(marker.Pos) == true;
        if (!marker.Data.ShouldShowDescription(targeted)) return;
        var text = GetDescription(marker);
        if (text == null) return;
        var (width, height) = SceneBubbleLayout.Size(text.Width, text.Height, RuntimeEnv.GUIScale, marker.Data.BubbleRenderScale);
        if (height <= 0) return;
        var render = _api.Render;
        var view = render.CameraMatrixOriginf;
        // Reserve the full focus/bob envelope so the bubble stays still through both animations.
        var offset = SceneDescriptionData.IndicatorBlocks * marker.Data.IndicatorScale * 1.1f / 2 + 0.15 + height / 2;
        var anchor = position.AddCopy(view[1] * offset, view[5] * offset, view[9] * offset);
        var centre = Project(anchor);
        if (centre.Z < 0) return;
        if (!VisibilityUtils.HasLineOfSight(_api.World, player.Entity, position, failOpen: true)) return;
        // One bubble height of world space at the anchor, in pixels: keeps the old quad's fixed
        // texel density per block without depending on the field of view.
        var pixelHeight = (float)Math.Abs(Project(anchor.AddCopy(view[1] * height, view[5] * height, view[9] * height)).Y - centre.Y);
        if (pixelHeight <= 0) return;
        var pixelWidth = pixelHeight * width / height;
        render.Render2DTexture(text.TextureId, (float)centre.X - pixelWidth / 2,
            render.FrameHeight - (float)centre.Y - pixelHeight / 2, pixelWidth, pixelHeight, 20f,
            new Vec4f(1, 1, 1, textOpacity));
    }

    private Vec3d Project(Vec3d worldPosition)
    {
        var render = _api.Render;
        return MatrixToolsd.Project(worldPosition, render.PerspectiveProjectionMat, render.PerspectiveViewMat, render.FrameWidth, render.FrameHeight);
    }

    private void RenderQuad(SceneDescriptionBlockEntity marker, LoadedTexture texture, Vec3d position, float opacity, float width, float height)
    {
        var render = _api.Render;
        if (_quad == null)
        {
            var mesh = QuadMeshUtil.GetQuad();
            mesh.Uv = [0, 1, 1, 1, 1, 0, 0, 0];
            mesh.Rgba = new byte[16];
            Array.Fill(mesh.Rgba, byte.MaxValue);
            _quad = render.UploadMesh(mesh);
        }
        var camera = _api.World.Player.Entity.CameraPos;
        var view = render.CameraMatrixOriginf;
        var model = _model.Identity().Values;
        // Inverse view rotation: the quad faces the camera at every pitch and yaw.
        model[0] = view[0] * width / 2; model[1] = view[4] * width / 2; model[2] = view[8] * width / 2;
        model[4] = view[1] * height / 2; model[5] = view[5] * height / 2; model[6] = view[9] * height / 2;
        model[8] = view[2]; model[9] = view[6]; model[10] = view[10];
        model[12] = (float)(position.X - camera.X);
        model[13] = (float)(position.Y - camera.Y);
        model[14] = (float)(position.Z - camera.Z);

        var shader = render.PreparedStandardShader(marker.Pos.X, marker.Pos.InternalY, marker.Pos.Z);
        shader.ViewMatrix = view;
        shader.ProjectionMatrix = render.CurrentProjectionMatrix;
        shader.ModelMatrix = model;
        shader.Tex2D = texture.TextureId;
        shader.NormalShaded = 0;
        shader.AlphaTest = 0.001f;
        // Straight-alpha blending also fades the standard shader's fog contribution.
        shader.RgbaTint = new Vec4f(1, 1, 1, opacity);
        try
        {
            render.RenderMesh(_quad);
        }
        finally
        {
            shader.Stop();
        }
    }

    private LoadedTexture GetDescription(SceneDescriptionBlockEntity marker)
    {
        _shownDescriptions.Add(marker);
        if (_descriptions.TryGetValue(marker, out var entry) && entry.Content == marker.Data.BubbleContent && entry.GuiScale == RuntimeEnv.GUIScale) return entry.Texture;
        RemoveDescription(marker);
        using var surface = SceneMarkerVisuals.DescriptionSurface(_api, marker.Data);
        if (surface == null) return null;
        var texture = new LoadedTexture(_api);
        _api.Gui.LoadOrUpdateCairoTexture(surface, false, ref texture);
        _descriptions[marker] = (marker.Data.BubbleContent, RuntimeEnv.GUIScale, texture);
        return texture;
    }

    private LoadedTexture GetIcon(SceneDescriptionData data)
    {
        var key = (data.SymbolIconKey, data.Color);
        if (_icons.TryGetValue(key, out var icon)) return icon;
        using var surface = new ImageSurface(Format.Argb32, 128, 128);
        using var ctx = new Context(surface);
        // A catalog icon that will not draw falls back to the enum symbol, never to an empty billboard.
        if (data.SymbolIconName.Length == 0 || !SceneTitleIcons.Draw(_api, ctx, surface, data.SymbolIconName, data.Color))
            SceneMarkerVisuals.Draw(ctx, SceneMarkerVisuals.LoadShape(_api, data.Symbol), 0, 0, 128, data.Color, data.Symbol);
        icon = new LoadedTexture(_api);
        _api.Gui.LoadOrUpdateCairoTexture(surface, true, ref icon);
        _icons.Add(key, icon);
        return icon;
    }

    public void Dispose()
    {
        _quad?.Dispose();
        _quad = null;
        foreach (var icon in _icons.Values) icon.Dispose();
        _icons.Clear();
        foreach (var entry in _descriptions.Values) entry.Texture?.Dispose();
        _descriptions.Clear();
        _shownDescriptions.Clear();
        _markers.Clear();
        _focus.Clear();
        _visible.Clear();
    }

    // An IRenderer carries one RenderOrder for every stage it is registered on, and the two passes
    // need different ones: icons after vanilla sign rendering in Opaque, bubbles before the GUI
    // draws at 1.0 in Ortho. Hence this forwarder rather than a second registration of the owner.
    private sealed class BubblePass : IRenderer
    {
        private readonly SceneMarkerIconRenderer _owner;
        internal BubblePass(SceneMarkerIconRenderer owner) => _owner = owner;
        public double RenderOrder => 0.42; // Just after placed chat bubbles, under the HUD and dialogs.
        public int RenderRange => int.MaxValue;
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage == EnumRenderStage.Ortho) _owner.RenderBubbles();
        }

        public void Dispose() { }
    }
}
