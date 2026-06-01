using System.Reflection;
using Vintagestory.API.Client;

namespace DimensionLib.ClientVisuals;

internal sealed class VanillaEffectSuppressor
{
    private readonly ICoreClientAPI _api;

    public VanillaEffectSuppressor(ICoreClientAPI api)
    {
        _api = api;
    }

    public void SuppressInheritedTemporalEffects(int? activeDimensionPlaneId)
    {
        var player = _api.World.Player;
        if (!activeDimensionPlaneId.HasValue || player?.Entity?.Pos.Dimension != activeDimensionPlaneId.Value)
        {
            return;
        }

        _api.Ambient.CurrentModifiers.Remove("brownrainandfog");
        _api.Ambient.CurrentModifiers.Remove("glitch");
        _api.Render.ShaderUniforms.GlitchStrength = 0f;
        _api.Render.ShaderUniforms.GlitchWaviness = 0f;
        _api.Render.ShaderUniforms.GlobalWorldWarp = 0f;

        if (player.Entity.WatchedAttributes != null)
        {
            player.Entity.WatchedAttributes.SetDouble("temporalStability", 1.0);
            player.Entity.WatchedAttributes.MarkPathDirty("temporalStability");
        }

        if (player.Entity.SidedProperties?.Behaviors == null)
        {
            return;
        }

        var behavior = player.Entity.GetBehavior("temporalstabilityaffected");
        if (behavior == null)
        {
            return;
        }

        SetDoubleField(behavior, "stabilityOffset", 20.0);
        SetDoubleField(behavior, "glitchEffectStrength", 0.0);
        SetDoubleField(behavior, "fogEffectStrength", 0.0);
        SetDoubleField(behavior, "rustPrecipColorStrength", 0.0);
        SetDoubleField(behavior, "hereTempStabChangeVelocity", 0.0);
        behavior.GetType().GetProperty("TempStabChangeVelocity")?.SetValue(behavior, 0.0);
    }

    public void SuppressVanillaCaveFog(bool isActive)
    {
        if (!isActive)
        {
            return;
        }

        if (_api.Ambient.CurrentModifiers.TryGetValue("blackfogincaves", out var blackFogInCaves))
        {
            blackFogInCaves.FogColor.Weight = 0f;
        }
    }

    private static void SetDoubleField(object target, string name, double value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(target, value);
    }
}
