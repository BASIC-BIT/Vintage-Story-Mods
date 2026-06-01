using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;

namespace DimensionLib.Generation;

internal sealed class DimensionGeneratorRegistry
{
    private readonly Dictionary<string, IDimensionGenerator> _generatorsById = new Dictionary<string, IDimensionGenerator>(StringComparer.Ordinal);

    public IReadOnlyCollection<string> GeneratorIds => _generatorsById.Keys.ToArray();

    public DimensionLibResult Register(IDimensionGenerator generator)
    {
        if (generator == null)
        {
            return DimensionLibResult.Fail("Generator is required.", "missing-generator");
        }

        var generatorId = generator.GeneratorId?.Trim();
        if (string.IsNullOrWhiteSpace(generatorId))
        {
            return DimensionLibResult.Fail("Generator id is required.", "missing-generator-id");
        }

        _generatorsById[generatorId] = generator;
        return DimensionLibResult.Ok($"Registered generator '{generatorId}'.");
    }

    public bool TryGet(string generatorId, out IDimensionGenerator generator)
    {
        return _generatorsById.TryGetValue(generatorId, out generator);
    }
}
