using System;
using Vintagestory.API.Client;

namespace DimensionLib.ClientVisuals;

internal sealed class AmbientModifierController
{
    private const string ModifierKey = "dimensionlib";

    private readonly ICoreClientAPI _api;
    private bool _applied;
    private int _appliedTuningRevision = -1;
    private string _appliedProfileId;

    public AmbientModifierController(ICoreClientAPI api)
    {
        _api = api;
    }

    public bool IsApplied => _applied;

    public bool Update(int? activeDimensionPlaneId, string activeProfileId, int tuningRevision, VisualTuningState tuning)
    {
        var player = _api.World.Player;
        if (activeDimensionPlaneId.HasValue && player?.Entity?.Pos.Dimension == activeDimensionPlaneId.Value)
        {
            return Apply(activeProfileId, tuningRevision, tuning);
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
        _appliedProfileId = null;
        _appliedTuningRevision = -1;
        return true;
    }

    private bool Apply(string profileId, int tuningRevision, VisualTuningState tuning)
    {
        if (_applied && string.Equals(_appliedProfileId, profileId, StringComparison.Ordinal) && _appliedTuningRevision == tuningRevision)
        {
            return false;
        }

        if (_applied)
        {
            Remove();
        }

        var modifier = VisualProfileRegistry.CreateModifier(profileId, tuning).EnsurePopulated();

        _api.Ambient.CurrentModifiers[ModifierKey] = modifier;
        _applied = true;
        _appliedProfileId = profileId;
        _appliedTuningRevision = tuningRevision;
        return true;
    }
}
