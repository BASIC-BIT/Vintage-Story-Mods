using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;

namespace DimensionLib.Services;

internal sealed class DimensionMappingRegistry
{
    private readonly Dictionary<string, DimensionMapping> _mappingsById = new Dictionary<string, DimensionMapping>(StringComparer.Ordinal);

    public IReadOnlyCollection<DimensionMapping> Mappings => _mappingsById.Values.ToArray();

    public DimensionLibResult<DimensionMapping> Register(DimensionMappingSpec spec)
    {
        var validation = DimensionMappingSpecValidator.Validate(spec);
        if (!validation.Success)
        {
            return DimensionLibResult<DimensionMapping>.Fail(validation.Message, validation.ErrorCode);
        }

        if (_mappingsById.TryGetValue(spec.MappingId, out var existing))
        {
            if (!DimensionMappingSpecValidator.SameMapping(existing, spec))
            {
                return DimensionLibResult<DimensionMapping>.Fail($"Mapping id '{spec.MappingId}' is already registered with different endpoints or transform.", "mapping-id-conflict");
            }

            var updated = spec.ToMapping();
            _mappingsById[updated.MappingId] = updated;
            return DimensionLibResult<DimensionMapping>.Ok(updated, "Mapping already registered; metadata refreshed.");
        }

        var mapping = spec.ToMapping();
        _mappingsById.Add(mapping.MappingId, mapping);
        return DimensionLibResult<DimensionMapping>.Ok(mapping, "Mapping registered.");
    }

    public DimensionLibResult<DimensionMapping> Get(string mappingId)
    {
        if (string.IsNullOrWhiteSpace(mappingId))
        {
            return DimensionLibResult<DimensionMapping>.Fail("Mapping id is required.", "missing-mapping-id");
        }

        return _mappingsById.TryGetValue(mappingId.Trim(), out var mapping)
            ? DimensionLibResult<DimensionMapping>.Ok(mapping)
            : DimensionLibResult<DimensionMapping>.Fail($"Mapping '{mappingId}' is not registered.", "unknown-mapping");
    }
}
