using System;

namespace thebasics.ModSystems.Teleportation;

internal sealed class TeleportGearRequirement
{
    private readonly bool _required;
    private readonly Func<bool> _isHoldingTemporalGear;
    private readonly Func<bool> _tryConsumeTemporalGear;

    public TeleportGearRequirement(bool required, Func<bool> isHoldingTemporalGear, Func<bool> tryConsumeTemporalGear)
    {
        _required = required;
        _isHoldingTemporalGear = isHoldingTemporalGear ?? throw new ArgumentNullException(nameof(isHoldingTemporalGear));
        _tryConsumeTemporalGear = tryConsumeTemporalGear ?? throw new ArgumentNullException(nameof(tryConsumeTemporalGear));
    }

    public bool CanBegin()
    {
        return !_required || _isHoldingTemporalGear();
    }

    public bool TryPayOnCompletion()
    {
        return !_required || _tryConsumeTemporalGear();
    }
}
