using System.Collections.Generic;
using DimensionLib.Api;

namespace DimensionLib.Protection;

internal sealed class PolicyProviderRegistry
{
    private readonly Dictionary<string, IDimensionPolicyProvider> _providersByOwnerModId = new Dictionary<string, IDimensionPolicyProvider>(System.StringComparer.Ordinal);

    public DimensionLibResult Register(string ownerModId, IDimensionPolicyProvider provider)
    {
        ownerModId = ownerModId?.Trim();
        if (string.IsNullOrWhiteSpace(ownerModId))
        {
            return DimensionLibResult.Fail("Owner mod id is required.", "missing-owner-mod-id");
        }

        if (provider == null)
        {
            return DimensionLibResult.Fail("Policy provider is required.", "missing-policy-provider");
        }

        _providersByOwnerModId[ownerModId] = provider;
        return DimensionLibResult.Ok($"Registered policy provider for '{ownerModId}'.");
    }

    public bool TryGet(Dimension dimension, out IDimensionPolicyProvider provider)
    {
        provider = null;
        return dimension != null && _providersByOwnerModId.TryGetValue(dimension.OwnerModId, out provider);
    }
}
