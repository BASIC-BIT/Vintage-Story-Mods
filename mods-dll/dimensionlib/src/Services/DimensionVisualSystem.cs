using System;
using System.Collections.Generic;
using System.Globalization;
using DimensionLib.Api;
using DimensionLib.ClientVisuals;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace DimensionLib.Services;

public sealed class DimensionVisualSystem : IRenderer
{
    private readonly ICoreClientAPI _api;
    private readonly AmbientModifierController _ambientModifierController;
    private readonly ScreenColorOverlayRenderer _overlayRenderer;
    private readonly VanillaEffectSuppressor _vanillaEffectSuppressor;
    private long _listenerId;
    private int? _activeDimensionPlaneId;
    private string _activeDimensionId;
    private DimensionVisualSettings _activeSettings;

    public DimensionVisualSystem(ICoreClientAPI api)
    {
        _api = api;
        _ambientModifierController = new AmbientModifierController(api);
        _overlayRenderer = new ScreenColorOverlayRenderer(api);
        _vanillaEffectSuppressor = new VanillaEffectSuppressor(api);
    }

    public void Start()
    {
        _listenerId = _api.Event.RegisterGameTickListener(OnGameTick, 20);
        _overlayRenderer.Start();
        _api.Event.RegisterRenderer(this, EnumRenderStage.Before, "dimensionlib-visual-suppression");
        _api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "dimensionlib-skycover");
        _api.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition, "dimensionlib-minlight");
    }

    public double RenderOrder => 0.35;

    public int RenderRange => 9999;

    public void Dispose()
    {
        if (_listenerId != 0)
        {
            _api.Event.UnregisterGameTickListener(_listenerId);
            _listenerId = 0;
        }

        _api.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        _api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        _api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        _overlayRenderer.Dispose();
        _ambientModifierController.Remove();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage == EnumRenderStage.Before)
        {
            _vanillaEffectSuppressor.SuppressVanillaCaveFog(ShouldApplyVisualEnvironment() && _activeSettings?.Fog.SuppressVanillaCaveFog == true);
            return;
        }

        if (stage == EnumRenderStage.AfterFinalComposition)
        {
            RenderMinimumSceneLightOverlay();
            return;
        }

        if (!ShouldRenderSkyCover())
        {
            return;
        }

        _overlayRenderer.RenderSkyCover(GetSkyCoverColor());
    }

    public void SetActiveVisualSettings(int dimensionPlaneId, string dimensionId, DimensionVisualSettings visualSettings)
    {
        if (visualSettings == null)
        {
            _activeDimensionPlaneId = null;
            _activeDimensionId = null;
            _activeSettings = null;
            _ambientModifierController.Remove();
            LogVisualState("transfer-clear");
            return;
        }

        _activeDimensionPlaneId = dimensionPlaneId;
        _activeDimensionId = string.IsNullOrWhiteSpace(dimensionId) ? null : dimensionId.Trim();
        _activeSettings = visualSettings.Clone();
        _activeSettings.Scene.MinimumLight = Clamp(_activeSettings.Scene.MinimumLight, 0f, 0.8f);
        UpdateAmbientModifier();
        LogVisualState("transfer");
    }

    private bool ShouldApplyVisualEnvironment()
    {
        var player = _api.World.Player;
        return _activeDimensionPlaneId.HasValue && player?.Entity?.Pos.Dimension == _activeDimensionPlaneId.Value;
    }

    private void OnGameTick(float dt)
    {
        if (UpdateAmbientModifier())
        {
            LogVisualState("ambient-update");
        }

        _vanillaEffectSuppressor.SuppressInheritedTemporalEffects(_activeDimensionPlaneId);
    }

    private bool ShouldRenderSkyCover()
    {
        return ShouldApplyVisualEnvironment() && _activeSettings?.Sky.RenderCover == true;
    }

    private bool UpdateAmbientModifier()
    {
        return _ambientModifierController.Update(_activeDimensionPlaneId, _activeSettings);
    }

    private void RenderMinimumSceneLightOverlay()
    {
        var effectiveMinimumSceneLight = _activeSettings?.Scene.MinimumLight ?? 0f;
        if (!ShouldApplyVisualEnvironment() || effectiveMinimumSceneLight <= 0f)
        {
            return;
        }

        var strength = Clamp(effectiveMinimumSceneLight, 0f, 0.3f);
        var liftColor = VisualSettingsMapper.GetLightLiftColor(_activeSettings);
        _overlayRenderer.RenderMinimumLightLift(new Vec4f(
            liftColor.X,
            liftColor.Y,
            liftColor.Z,
            strength));
    }

    private Vec4f GetSkyCoverColor()
    {
        var skyColor = VisualSettingsMapper.GetSkyColor(_activeSettings);
        return new Vec4f(
            skyColor.X,
            skyColor.Y,
            skyColor.Z,
            VisualSettingsMapper.GetSkyAlpha(_activeSettings));
    }

    private void LogVisualState(string reason)
    {
        _api.Logger.Notification(BuildVisualStateSummary(reason));
    }

    private string BuildVisualStateSummary(string reason)
    {
        var playerDimension = _api.World.Player?.Entity?.Pos.Dimension;
        var settings = _activeSettings ?? new DimensionVisualSettings();
        var minimumSceneLight = settings.Scene.MinimumLight;
        var liftStrength = Clamp(minimumSceneLight, 0f, 0.3f);
        var sky = GetSkyCoverColor();

        return string.Format(
            CultureInfo.InvariantCulture,
            "[DimensionLib] Visual state ({0}): playerDim={1}, activePlane={2}, dimension={3}, visualSettings={4}, applies={5}, ambientApplied={6}, minimumSceneLight={7:0.###}, liftStrength={8:0.###}, sky=({9:0.###},{10:0.###},{11:0.###},{12:0.###}), fog=({13:0.###},{14:0.###},{15:0.###}) weight={16:0.###} density={17:0.####} densityWeight={18:0.###} flatDensity={19:0.####} flatWeight={20:0.###}, ambient=({21:0.###},{22:0.###},{23:0.###}) weight={24:0.###}, sceneBrightness={25:0.###} sceneWeight={26:0.###}, fogBrightness={27:0.###} fogBrightnessWeight={28:0.###}",
            reason,
            playerDimension.HasValue ? playerDimension.Value.ToString(CultureInfo.InvariantCulture) : "none",
            _activeDimensionPlaneId.HasValue ? _activeDimensionPlaneId.Value.ToString(CultureInfo.InvariantCulture) : "none",
            _activeDimensionId ?? "none",
            _activeSettings == null ? "none" : "explicit",
            ShouldApplyVisualEnvironment(),
            _ambientModifierController.IsApplied,
            minimumSceneLight,
            liftStrength,
            sky.X,
            sky.Y,
            sky.Z,
            sky.W,
            settings.Fog.Color.Value.Red,
            settings.Fog.Color.Value.Green,
            settings.Fog.Color.Value.Blue,
            settings.Fog.Color.Weight,
            settings.Fog.Density.Value,
            settings.Fog.Density.Weight,
            settings.Fog.FlatDensity.Value,
            settings.Fog.FlatDensity.Weight,
            settings.Ambient.Color.Value.Red,
            settings.Ambient.Color.Value.Green,
            settings.Ambient.Color.Value.Blue,
            settings.Ambient.Color.Weight,
            settings.Scene.Brightness.Value,
            settings.Scene.Brightness.Weight,
            settings.Fog.Brightness.Value,
            settings.Fog.Brightness.Weight);
    }

    private static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

}
