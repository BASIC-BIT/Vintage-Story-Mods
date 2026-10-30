using System;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A detached guide actor, rendered by the client or the standalone asset renderer.</summary>
public interface ISetupGuidePreview : IDisposable
{
    bool IsReady { get; }
    void PlayAnimation(string code);
    void SetAnimation(string code, double timeSeconds);
    void BeginDrag(double x);
    void DragTo(double x);
    void EndDrag(bool allowMomentum = true);
    void Render(float dt, ElementBounds bounds, float opacity = 1, float yawOffset = 0);
}

/// <summary>One actor in logical scene coordinates, with a deterministic animation time.</summary>
public readonly record struct SetupWizardActorSample(string Slot, string Identity, string Animation, double AnimationTime,
    double X, double Y, double Width, double Height, float Opacity = 1, float YawOffset = 0);
