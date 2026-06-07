using System;
using DimensionLib.Api;

namespace DimensionLib.Services;

internal static class DimensionMappingSpecValidator
{
    public static DimensionLibResult Validate(DimensionMappingSpec spec)
    {
        if (spec == null)
        {
            return DimensionLibResult.Fail("Dimension mapping spec is required.", "missing-mapping-spec");
        }

        spec.MappingId = spec.MappingId?.Trim();
        spec.OwnerModId = spec.OwnerModId?.Trim();
        spec.SourceDimensionId = spec.SourceDimensionId?.Trim();
        spec.TargetDimensionId = spec.TargetDimensionId?.Trim();
        spec.Transform ??= DimensionMappingTransform.Identity();

        if (string.IsNullOrWhiteSpace(spec.MappingId))
        {
            return DimensionLibResult.Fail("Mapping id is required.", "missing-mapping-id");
        }

        if (string.IsNullOrWhiteSpace(spec.OwnerModId))
        {
            return DimensionLibResult.Fail("Owner mod id is required.", "missing-owner-mod-id");
        }

        if (string.IsNullOrWhiteSpace(spec.SourceDimensionId))
        {
            return DimensionLibResult.Fail("Source dimension id is required.", "missing-source-dimension-id");
        }

        if (string.IsNullOrWhiteSpace(spec.TargetDimensionId))
        {
            return DimensionLibResult.Fail("Target dimension id is required.", "missing-target-dimension-id");
        }

        return ValidateTransform(spec.Transform);
    }

    public static bool SameMapping(DimensionMapping existing, DimensionMappingSpec spec)
    {
        return string.Equals(existing.OwnerModId, spec.OwnerModId, StringComparison.Ordinal) &&
            string.Equals(existing.SourceDimensionId, spec.SourceDimensionId, StringComparison.Ordinal) &&
            string.Equals(existing.TargetDimensionId, spec.TargetDimensionId, StringComparison.Ordinal) &&
            existing.Bidirectional == spec.Bidirectional &&
            existing.IsTransient == spec.IsTransient &&
            SameTransform(existing.Transform, spec.Transform);
    }

    private static DimensionLibResult ValidateTransform(DimensionMappingTransform transform)
    {
        if (!IsFiniteNonZero(transform.ScaleX) || !IsFiniteNonZero(transform.ScaleY) || !IsFiniteNonZero(transform.ScaleZ))
        {
            return DimensionLibResult.Fail("Dimension mapping scale values must be finite and non-zero.", "invalid-mapping-scale");
        }

        if (!IsFinite(transform.OffsetX) || !IsFinite(transform.OffsetY) || !IsFinite(transform.OffsetZ))
        {
            return DimensionLibResult.Fail("Dimension mapping offset values must be finite.", "invalid-mapping-offset");
        }

        return DimensionLibResult.Ok();
    }

    private static bool SameTransform(DimensionMappingTransform left, DimensionMappingTransform right)
    {
        return AreSame(left.ScaleX, right.ScaleX) &&
            AreSame(left.ScaleY, right.ScaleY) &&
            AreSame(left.ScaleZ, right.ScaleZ) &&
            AreSame(left.OffsetX, right.OffsetX) &&
            AreSame(left.OffsetY, right.OffsetY) &&
            AreSame(left.OffsetZ, right.OffsetZ);
    }

    private static bool AreSame(double left, double right)
    {
        return Math.Abs(left - right) < 0.000001;
    }

    private static bool IsFiniteNonZero(double value)
    {
        return IsFinite(value) && Math.Abs(value) > 0.000001;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
