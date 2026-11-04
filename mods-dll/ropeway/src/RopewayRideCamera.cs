using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Ropeway;

/// <summary>
/// The outside view: third person while you are aboard the cabin, your own camera back when you get off.
///
/// Client side only, no packets, no seat changes. The camera mode is not a setting - <c>Camera.CameraMode</c>
/// and <c>Camera.SetMode</c> are both internal (VintagestoryLib/Camera.cs:32,132) and there is no
/// ClientSettings key for it - so it is READ through <see cref="IRenderAPI.CameraType"/> and WRITTEN by
/// invoking vanilla's own "cyclecamera" hotkey handler, which is a public field on a public HotKey
/// (PlayerCamera.cs:31, HotKey.Handler) and ignores its argument (PlayerCamera.cs:132). That beats the
/// reflection the prior art on ModDB uses and costs no extra assembly reference.
///
/// Read the mode from Render.CameraType, NOT IClientPlayer.CameraMode: the latter is
/// <c>OverrideCameraMode ?? MainCamera.CameraMode</c> and vanilla transiently forces the override to
/// FirstPerson whenever the third person camera is wall-clipped (Camera.cs:168), so it lies about the
/// real mode - and lying here would make us "restore" a camera we never set.
/// </summary>
public sealed class RopewayRideCamera
{
    /// <summary>
    /// O is unbound in vanilla - the full registered set is HotkeyManager plus the three content mods, and
    /// it leaves only I, K, L, O, P, R and U free. R is already the stop key.
    /// </summary>
    public const string Hotkey = "ropewayridecam";

    private const string VanillaCycleCamera = "cyclecamera";

    private readonly ICoreClientAPI capi;

    /// <summary>The player asked for the outside view. Survives a dismount; a session is as long as it gets.</summary>
    private bool wanted;

    /// <summary>The camera is in third person BECAUSE OF US, so we owe them a restore.</summary>
    private bool applied;

    /// <summary>They moved the camera themselves mid-ride. Do not fight them; reset when they get off.</summary>
    private bool handsOff;

    public RopewayRideCamera(ICoreClientAPI capi)
    {
        this.capi = capi;

        capi.Input.RegisterHotKey(Hotkey, Lang.Get("ropeway:hotkey-ridecam"), GlKeys.O, HotkeyType.CharacterControls);
        capi.Input.SetHotKeyHandler(Hotkey, _ =>
        {
            // Not aboard our cabin: hand the key straight back, so it stays free for anything else bound to
            // it. Same contract as the stop key.
            if (!Riding) return false;

            wanted = !wanted;
            handsOff = false;
            Poll(0);
            return true;
        });

        // Polling MountedOn catches every way a ride can end - normal dismount, death, teleport, the cabin
        // despawning or its chunk unloading - where hooking our own seat's DidUnmount catches only the first.
        capi.Event.RegisterGameTickListener(Poll, 250);
    }

    private bool Riding => capi.World?.Player?.Entity?.MountedOn?.Entity is EntityRopewayCabin;

    private void Poll(float dt)
    {
        var mode = capi.Render.CameraType;

        // F5, or another mod. Either way it is no longer our camera to put back.
        if (applied && mode != EnumCameraMode.ThirdPerson)
        {
            applied = false;
            handsOff = true;
        }

        if (wanted && Riding)
        {
            // Only ever from first person: mount in third person already and we neither switch nor restore.
            if (!applied && !handsOff && mode == EnumCameraMode.FirstPerson)
            {
                applied = Set(EnumCameraMode.ThirdPerson);
            }

            return;
        }

        handsOff = false;
        if (!applied) return;

        Set(EnumCameraMode.FirstPerson);
        applied = false;
    }

    /// <summary>
    /// Cycles vanilla's own handler until the mode matches. Three stops in the rotation, so three tries is
    /// the whole cycle; if another mod has replaced the handler and it never converges, give up rather than
    /// spin, and report failure so we do not claim a restore we never made.
    /// </summary>
    private bool Set(EnumCameraMode target)
    {
        var handler = capi.Input.GetHotKeyByCode(VanillaCycleCamera)?.Handler;
        if (handler == null) return false;

        for (var i = 0; i < 3 && capi.Render.CameraType != target; i++) handler(null);

        return capi.Render.CameraType == target;
    }
}
