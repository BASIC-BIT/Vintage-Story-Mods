using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Config;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneMarkerIconRenderer : IRenderer
{
    private const float MaxDescriptionHeight = 6;
    private const double DescriptionCullRadius = MaxDescriptionHeight + 2;
    private readonly ICoreClientAPI _api;
    private readonly HashSet<SceneDescriptionBlockEntity> _markers = new();
    private readonly Dictionary<(SceneMarkerSymbol Symbol, SceneMarkerColor Color), LoadedTexture> _icons = new();
    private readonly List<(SceneDescriptionBlockEntity Marker, Vec3d Position, float Opacity, float TextOpacity, float Focus, double Depth)> _visible = new();
    private readonly Dictionary<SceneDescriptionBlockEntity, (SceneDescriptionData Data, LoadedTexture Texture)> _descriptions = new();
    private readonly HashSet<SceneDescriptionBlockEntity> _shownDescriptions = new();
    private readonly Dictionary<SceneDescriptionBlockEntity, float> _focus = new();
    private readonly Matrixf _model = new();
    private MeshRef _quad;

    internal SceneMarkerIconRenderer(ICoreClientAPI api) => _api = api;
    public double RenderOrder => 1.11; // After terrain and vanilla sign rendering.
    public int RenderRange => int.MaxValue;
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
        var player = _api.World?.Player?.Entity;
        if (stage != EnumRenderStage.Opaque || player == null || _markers.Count == 0) return;
        var render = _api.Render;
        GatherVisibleIcons(deltaTime);
        _shownDescriptions.Clear();
        if (_visible.Count == 0)
        {
            foreach (var marker in _descriptions.Keys.ToArray()) RemoveDescription(marker);
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
                var size = 0.8f * (1 + 0.10f * icon.Focus);
                if (icon.Opacity > 0) RenderQuad(icon.Marker, GetIcon(icon.Marker.Data.Symbol, icon.Marker.Data.Color), icon.Position, icon.Opacity, size, size);
                var targeted = _api.World.Player.CurrentBlockSelection?.Position?.Equals(icon.Marker.Pos) == true;
                if (!icon.Marker.Data.ShouldShowDescription(targeted) || icon.TextOpacity <= 0) continue;
                var text = GetDescription(icon.Marker);
                if (text == null) continue;
                var width = (float)Math.Min(3, _api.World.Player.Entity.CameraPos.DistanceTo(icon.Position) * 0.45);
                var height = width * text.Height / text.Width;
                if (height > MaxDescriptionHeight)
                {
                    width *= MaxDescriptionHeight / height;
                    height = MaxDescriptionHeight;
                }
                var view = render.CameraMatrixOriginf;
                var offset = size / 2 + 0.1 + height / 2;
                var textPosition = icon.Position.AddCopy(view[1] * offset, view[5] * offset, view[9] * offset);
                var textOpacity = icon.TextOpacity;
                RenderQuad(icon.Marker, text, textPosition, textOpacity, width, height);
            }
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
            var opacity = marker.Data.GetIconOpacity(distance);
            position.Y += marker.Data.HeightOffset;
            // Four-second cycle, only five centimetres either side of the resting height.
            position.Y += 0.05 * Math.Sin(_api.World.ElapsedMilliseconds * (Math.PI * 2 / 4000));
            var selected = _api.World.Player.CurrentBlockSelection?.Position?.Equals(marker.Pos) == true;
            var targeted = selected && marker.Data.Display == SceneDescriptionDisplay.WhenTargeted && marker.Data.ShouldShowDescription(true);
            var textOpacity = targeted ? 1 : marker.Data.ShouldShowDescription(selected) ? marker.Data.GetTextOpacity(distance) : 0;
            var focus = _focus.GetValueOrDefault(marker);
            focus += ((selected ? 1 : 0) - focus) * (1 - MathF.Exp(-10 * Math.Max(0, deltaTime)));
            _focus[marker] = focus;
            var radius = textOpacity > 0 ? DescriptionCullRadius : 1;
            if ((opacity <= 0 && textOpacity <= 0) || !render.DefaultFrustumCuller.SphereInFrustum(position.X, position.Y, position.Z, radius)) continue;
            var camera = player.CameraPos;
            var view = render.CameraMatrixOriginf;
            var depth = -(view[2] * (position.X - camera.X) + view[6] * (position.Y - camera.Y) + view[10] * (position.Z - camera.Z));
            _visible.Add((marker, position, opacity, textOpacity, focus, depth));
        }
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
        if (_descriptions.TryGetValue(marker, out var entry) && ReferenceEquals(entry.Data, marker.Data)) return entry.Texture;
        RemoveDescription(marker);
        using var surface = SceneMarkerVisuals.DescriptionSurface(_api, marker.Data);
        if (surface == null) return null;
        var texture = new LoadedTexture(_api);
        _api.Gui.LoadOrUpdateCairoTexture(surface, false, ref texture);
        _descriptions[marker] = (marker.Data, texture);
        return texture;
    }

    private LoadedTexture GetIcon(SceneMarkerSymbol symbol, SceneMarkerColor color)
    {
        if (_icons.TryGetValue((symbol, color), out var icon)) return icon;
        using var surface = new ImageSurface(Format.Argb32, 128, 128);
        using var ctx = new Context(surface);
        SceneMarkerVisuals.Draw(ctx, SceneMarkerVisuals.LoadShape(_api, symbol), 0, 0, 128, color);
        icon = new LoadedTexture(_api);
        _api.Gui.LoadOrUpdateCairoTexture(surface, true, ref icon);
        _icons.Add((symbol, color), icon);
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
}
