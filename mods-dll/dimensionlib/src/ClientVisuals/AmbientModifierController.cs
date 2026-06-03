using System;
using DimensionLib.Api;
using Vintagestory.API.Client;

namespace DimensionLib.ClientVisuals;

internal sealed class AmbientModifierController
{
    private const string ModifierKey = "dimensionlib";

    private readonly ICoreClientAPI _api;
    private bool _applied;
    private DimensionVisualSettings _appliedSettings;

    public AmbientModifierController(ICoreClientAPI api)
    {
        _api = api;
    }

    public bool IsApplied => _applied;

    public bool Update(int? activeDimensionPlaneId, DimensionVisualSettings activeSettings)
    {
        var player = _api.World.Player;
        if (activeDimensionPlaneId.HasValue && activeSettings != null && player?.Entity?.Pos.Dimension == activeDimensionPlaneId.Value)
        {
            return Apply(activeSettings);
        }

        return Remove();
    }

    public bool Remove()
    {
        if (!_applied)
        {
            return false;
        }

        _api.Ambient.CurrentModifiers.Remove(ModifierKey);
        _applied = false;
        _appliedSettings = null;
        return true;
    }

    private bool Apply(DimensionVisualSettings settings)
    {
        if (_applied && ReferenceEquals(_appliedSettings, settings))
        {
            return false;
        }

        if (_applied)
        {
            Remove();
        }

        var modifier = VisualSettingsMapper.CreateModifier(settings).EnsurePopulated();

        _api.Ambient.CurrentModifiers[ModifierKey] = modifier;
        _applied = true;
        _appliedSettings = settings;
        return true;
    }
}
