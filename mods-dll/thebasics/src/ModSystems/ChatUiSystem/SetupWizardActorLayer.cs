using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class SetupWizardActorPool(ICoreClientAPI api, Func<ICoreClientAPI, string, ISetupGuidePreview> factory) : IDisposable
{
    private readonly Dictionary<(string Slot, string Identity), ISetupGuidePreview> _actors = new();
    internal ISetupGuidePreview Get(string slot, string identity)
    {
        var key = (slot, identity);
        if (!_actors.TryGetValue(key, out var actor))
        {
            actor = factory(api, identity) ?? throw new InvalidOperationException("The setup actor factory returned no actor.");
            _actors.Add(key, actor);
        }
        return actor;
    }
    public void Dispose()
    {
        foreach (var actor in _actors.Values.Distinct()) actor.Dispose();
        _actors.Clear();
    }
}

/// <summary>Renders the same actor requests through the native or standalone guide implementation.</summary>
internal sealed class SetupWizardActorLayer : GuiElement
{
    private sealed class State
    {
        public string Animation;
        public double Time = -1;
        public bool Pinned;
    }
    private readonly SetupWizardActorPool _pool;
    private readonly Func<SetupWizardActorSample[]> _samples;
    private readonly Func<bool> _pinned;
    private readonly Dictionary<(string, string), State> _states = new();

    internal SetupWizardActorLayer(ICoreClientAPI api, ElementBounds bounds, SetupWizardActorPool pool,
        Func<SetupWizardActorSample[]> samples, Func<bool> pinned) : base(api, bounds)
    {
        _pool = pool;
        _samples = samples;
        _pinned = pinned;
    }

    internal bool IsReady => _samples().All(sample => _pool.Get(sample.Slot, sample.Identity).IsReady);

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        foreach (var sample in _samples()) _pool.Get(sample.Slot, sample.Identity);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        Bounds.CalcWorldBounds();
        var pinned = _pinned();
        api.Render.PushScissor(Bounds, api.Render.ScissorStack.Count > 0 && api.Render.ScissorStack.Peek() != null);
        try
        {
            foreach (var sample in _samples())
            {
                var actor = _pool.Get(sample.Slot, sample.Identity);
                var key = (sample.Slot, sample.Identity);
                if (!_states.TryGetValue(key, out var state)) _states.Add(key, state = new());
                var time = Math.Clamp(sample.AnimationTime, 0, 60);
                if (state.Animation != sample.Animation || time + 0.05 < state.Time || pinned && (!state.Pinned || time != state.Time))
                {
                    actor.SetAnimation(sample.Animation, time);
                    if (!pinned) actor.PlayAnimation(sample.Animation);
                }
                else if (!pinned && state.Pinned) actor.PlayAnimation(sample.Animation);
                state.Animation = sample.Animation;
                state.Time = time;
                state.Pinned = pinned;
                var actorBounds = ElementBounds.Fixed(sample.X, sample.Y, sample.Width, sample.Height).WithParent(Bounds);
                actorBounds.CalcWorldBounds();
                actor.Render(pinned ? 0 : deltaTime, actorBounds, sample.Opacity, sample.YawOffset);
            }
        }
        finally { api.Render.PopScissor(); }
    }
}
