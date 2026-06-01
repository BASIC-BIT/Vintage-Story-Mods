using System;
using DimensionLib.Api;
using Vintagestory.API.Client;

namespace DimensionLib.ClientVisuals;

internal sealed class AmbientModifierController
{
    private const string ModifierKey = "dimensionlib";

    private readonly ICoreClientAPI _api;
    private bool _applied;
    private int _appliedTuningRevision = -1;
    private DimensionVisualSettings _appliedSettings;

    public AmbientModifierController(ICoreClientAPI api)
    {
        _api = api;
    }

    public bool IsApplied => _applied;

    public bool Update(int? activeDimensionPlaneId, DimensionVisualSettings activeSettings, int tuningRevision, VisualTuningState tuning)
    {
        var player = _api.World.Player;
        if (activeDimensionPlaneId.HasValue && activeSettings != null && player?.Entity?.Pos.Dimension == activeDimensionPlaneId.Value)
        {
            return Apply(activeSettings, tuningRevision, tuning);
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
        _appliedTuningRevision = -1;
        return true;
    }

    private bool Apply(DimensionVisualSettings settings, int tuningRevision, VisualTuningState tuning)
    {
        if (_applied && ReferenceEquals(_appliedSettings, settings) && _appliedTuningRevision == tuningRevision)
        {
            return false;
        }

        if (_applied)
        {
            Remove();
        }

        var modifier = VisualSettingsMapper.CreateModifier(settings, tuning).EnsurePopulated();

        _api.Ambient.CurrentModifiers[ModifierKey] = modifier;
        _applied = true;
        _appliedSettings = settings;
        _appliedTuningRevision = tuningRevision;
        return true;
    }
}
