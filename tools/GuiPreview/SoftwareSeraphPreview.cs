using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace TheBasics.GuiPreview;

/// <summary>Draws the game's clothed, animated seraph mesh without a client or graphics context.</summary>
public sealed class SoftwareSeraphPreview(ICoreClientAPI api, SoftwareCanvas canvas, SeraphModel model, string identity) : ISetupGuidePreview
{
    private const float DefaultYaw = -1.2707963f;
    private string animation = "idle";
    private double time, dragX;
    private long dragTime;
    private float yaw = DefaultYaw, velocity;
    private bool pinned, dragging, disposed;
    private int texture;
    private (string Animation, double Time, int Width, int Height, float Yaw)? rendered;
    public bool IsReady => !disposed;

    public void PlayAnimation(string code)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!pinned && animation == code) return;
        model.Pose(code, 0);
        if (animation != code) time = 0;
        animation = code;
        pinned = false;
    }

    public void SetAnimation(string code, double timeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0 || timeSeconds > 60)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        model.Pose(code, timeSeconds);
        animation = code;
        time = timeSeconds;
        pinned = true;
        yaw = DefaultYaw;
        velocity = 0;
        dragging = false;
    }

    public void BeginDrag(double x)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (pinned) return;
        dragging = true;
        dragX = x;
        dragTime = api.ElapsedMilliseconds;
        velocity = 0;
    }

    public void DragTo(double x)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!dragging) return;
        long elapsed = api.ElapsedMilliseconds - dragTime;
        double delta = -(x - dragX) / 100;
        if (double.IsFinite(delta))
        {
            yaw = (float)Math.IEEERemainder(yaw + delta, Math.Tau);
            velocity = elapsed > 0 && elapsed <= 100 ? (float)Math.Clamp(delta * 1000 / elapsed, -12, 12)
                : elapsed == 0 ? velocity : 0;
        }
        else velocity = 0;
        dragX = x;
        dragTime = api.ElapsedMilliseconds;
    }

    public void EndDrag(bool allowMomentum = true)
    {
        if (disposed) return;
        long elapsed = api.ElapsedMilliseconds - dragTime;
        if (!allowMomentum || elapsed < 0 || elapsed > 100) velocity = 0;
        dragging = false;
    }

    public void Render(float dt, ElementBounds bounds, float opacity = 1, float yawOffset = 0)
    {
        if (disposed || bounds == null) return;
        if (!float.IsFinite(opacity) || !float.IsFinite(yawOffset)) throw new ArgumentException("Actor opacity and rotation must be finite.");
        if (!pinned && float.IsFinite(dt) && dt > 0)
        {
            time += Math.Min(dt, 0.25f);
            if (!dragging && velocity != 0)
            {
                float decay = MathF.Exp(-6 * Math.Min(dt, 0.05f));
                yaw = (float)Math.IEEERemainder(yaw + velocity * (1 - decay) / 6, Math.Tau);
                velocity *= decay;
                if (MathF.Abs(velocity) < 0.01f) velocity = 0;
            }
        }
        int width = bounds.OuterWidthInt, height = bounds.OuterHeightInt;
        if (width <= 0 || height <= 0 || opacity <= 0) return;
        var frame = (animation, time, width, height, yaw + yawOffset);
        if (rendered != frame)
        {
            var pixels = SeraphRasterizer.Render(model, model.Pose(animation, time), width, height, frame.Item5, fitPosed: animation == "sleep");
            texture = canvas.UploadTexture(pixels, width, height, checked(width * 4), true, texture);
            rendered = frame;
        }
        opacity = Math.Clamp(opacity, 0, 1);
        canvas.DrawTexture(texture, bounds.renderX, bounds.renderY, width, height, 100,
            new Vec4f(opacity, opacity, opacity, opacity), premultipliedAlpha: true);
        canvas.Commands.Add(new { op = "seraph", identity, animation, time, yaw = frame.Item5, opacity, width, height,
            vertices = model.Mesh.VerticesCount, indices = model.Mesh.IndicesCount });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (texture != 0) canvas.DeleteTexture(texture);
        texture = 0;
    }
}
