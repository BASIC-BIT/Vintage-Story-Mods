using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneMarkerIconRenderer : IRenderer
{
    private readonly ICoreClientAPI _api;
    private readonly HashSet<SceneDescriptionBlockEntity> _markers = new();
    private readonly Dictionary<SceneMarkerSymbol, LoadedTexture> _icons = new();
    private readonly List<(SceneDescriptionBlockEntity Marker, Vec3d Position, float Opacity, double Distance)> _visible = new();
    private readonly Matrixf _model = new();
    private MeshRef _quad;

    internal SceneMarkerIconRenderer(ICoreClientAPI api) => _api = api;
    public double RenderOrder => 1.11; // After terrain and vanilla sign rendering.
    public int RenderRange => int.MaxValue;
    internal void Register(SceneDescriptionBlockEntity marker) => _markers.Add(marker);
    internal void Unregister(SceneDescriptionBlockEntity marker) => _markers.Remove(marker);

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var player = _api.World?.Player?.Entity;
        if (stage != EnumRenderStage.Opaque || player == null || _markers.Count == 0) return;
        var render = _api.Render;
        GatherVisibleIcons();
        if (_visible.Count == 0) return;
        // Transparent symbols must blend back-to-front because they do not write terrain depth.
        _visible.Sort((left, right) => right.Distance.CompareTo(left.Distance));
        var previousShader = render.CurrentActiveShader;
        previousShader?.Stop();
        try
        {
            render.GLEnableDepthTest();
            render.GLDepthMask(false);
            render.GlDisableCullFace();
            render.GlToggleBlend(true, EnumBlendMode.Standard);
            foreach (var icon in _visible) RenderIcon(icon.Marker, icon.Position, icon.Opacity);
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

    private void GatherVisibleIcons()
    {
        var player = _api.World.Player.Entity;
        var render = _api.Render;
        _visible.Clear();
        foreach (var marker in _markers)
        {
            if (marker.Pos.dimension != player.Pos.Dimension) continue;
            var position = marker.Pos.ToVec3d().Add(0.5, 0.65, 0.5);
            var opacity = marker.Data.GetIconOpacity(player.Pos.XYZ.DistanceTo(position));
            // Four-second cycle, only five centimetres either side of the resting height.
            position.Y += 0.05 * Math.Sin(_api.World.ElapsedMilliseconds * (Math.PI * 2 / 4000));
            if (opacity <= 0 || !render.DefaultFrustumCuller.SphereInFrustum(position.X, position.Y, position.Z, 1)) continue;
            _visible.Add((marker, position, opacity, player.CameraPos.SquareDistanceTo(position)));
        }
    }

    private void RenderIcon(SceneDescriptionBlockEntity marker, Vec3d position, float opacity)
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
        var icon = GetIcon(marker.Data.Symbol);
        var camera = _api.World.Player.Entity.CameraPos;
        var view = render.CameraMatrixOriginf;
        var model = _model.Identity().Values;
        // Inverse view rotation: the quad faces the camera at every pitch and yaw.
        model[0] = view[0] * 0.4f; model[1] = view[4] * 0.4f; model[2] = view[8] * 0.4f;
        model[4] = view[1] * 0.4f; model[5] = view[5] * 0.4f; model[6] = view[9] * 0.4f;
        model[8] = view[2]; model[9] = view[6]; model[10] = view[10];
        model[12] = (float)(position.X - camera.X);
        model[13] = (float)(position.Y - camera.Y);
        model[14] = (float)(position.Z - camera.Z);

        var shader = render.PreparedStandardShader(marker.Pos.X, marker.Pos.InternalY, marker.Pos.Z);
        shader.ViewMatrix = view;
        shader.ProjectionMatrix = render.CurrentProjectionMatrix;
        shader.ModelMatrix = model;
        shader.Tex2D = icon.TextureId;
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

    private LoadedTexture GetIcon(SceneMarkerSymbol symbol)
    {
        if (_icons.TryGetValue(symbol, out var icon)) return icon;
        using var surface = new ImageSurface(Format.Argb32, 128, 128);
        using var ctx = new Context(surface);
        SceneMarkerVisuals.Draw(ctx, SceneMarkerVisuals.LoadShape(_api, symbol), 0, 0, 128);
        icon = new LoadedTexture(_api);
        _api.Gui.LoadOrUpdateCairoTexture(surface, true, ref icon);
        _icons.Add(symbol, icon);
        return icon;
    }

    public void Dispose()
    {
        _quad?.Dispose();
        _quad = null;
        foreach (var icon in _icons.Values) icon.Dispose();
        _icons.Clear();
        _markers.Clear();
        _visible.Clear();
    }
}
