using System;
using System.Globalization;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A local travel example. Time is in real seconds; no player or world is changed.</summary>
public static class SetupWizardTravelPreview
{
    private enum Phase { Disabled, Request, Warmup, Warp, Arrived, Cancelled, Expired, Blocked }
    private readonly record struct Frame(string Command, Phase Phase, string Text, double Remaining, bool Gear, bool Paid, double Time,
        double ArrivalTime = 0);

    public static bool Supports(string pageId) => pageId is
        "teleport.tools" or "teleport.utilities" or "teleport.requests" or "teleport.requesttiming" or
        "teleport.homes" or "teleport.spawn" or "teleport.top" or "teleport.back" or "teleport.stuck" or
        "teleport.stuckpolicy" or "teleport.warmup";

    public static string Message(string pageId, SetupWizardDraft draft, double time) =>
        Supports(pageId) && draft != null ? Sample(pageId, draft, Time(time)).Text : "";

    public static SetupWizardActorSample[] Actors(string pageId, SetupWizardDraft draft, double time, double width, double height)
    {
        if (!Supports(pageId) || draft == null || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return [];
        var frame = Sample(pageId, draft, Time(time));
        var floor = height - 38;
        var arrived = frame.Phase == Phase.Arrived;
        var x = arrived ? 244 : 65;
        if (frame.Phase == Phase.Cancelled) x += 8;
        var y = arrived && frame.Command == "top" && height >= 150 ? floor - 23 : floor;
        var opacity = frame.Phase is Phase.Disabled or Phase.Blocked or Phase.Expired ? 0.55 :
            frame.Phase == Phase.Warp ? Math.Abs(Math.Cos(frame.Time * Math.PI * 3)) : 1;
        var animationTime = arrived ? frame.Time - frame.ArrivalTime : frame.Time;
        return [new("pip", "Pip", arrived ? "wave" : "idle", animationTime % 60, x - 21, y - 82, 42, 70, (float)opacity)];
    }

    public static double DurationSeconds(string pageId, SetupWizardDraft draft)
    {
        if (pageId is "teleport.tools" or "teleport.utilities") return 24;
        if (pageId == "teleport.warmup") return 12;
        if (pageId == "teleport.requesttiming")
            return On(draft, "TpaUseTimeout") ? Math.Max(8, Number(draft, "TpaTimeoutMinutes") * 60 + 3) : 12;
        var command = pageId switch
        {
            "teleport.requests" => "Tpa", "teleport.homes" => "Home", "teleport.stuckpolicy" => "Stuck",
            _ => char.ToUpperInvariant(pageId[9]) + pageId[10..]
        };
        return Number(draft, "Teleportation." + command + "WarmupSeconds") + (command == "Tpa" ? 5 : 4);
    }

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft, double time)
    {
        if (!Supports(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        var frame = Sample(pageId, draft, Time(time));
        var height = surface.Height / Math.Max(1d, surface.Width) < 0.5 ? 130d : 180d;
        var scale = Math.Min(surface.Width / 310d, surface.Height / height);
        context.Save();
        try
        {
            context.Rectangle(0, 0, surface.Width, surface.Height); context.Clip();
            context.Translate((surface.Width - 310 * scale) / 2, (surface.Height - height * scale) / 2);
            context.Scale(scale, scale);
            var floor = height - 38;
            var destinationY = frame.Command == "top" && height >= 150 ? floor - 23 : floor;
            var blocked = frame.Phase is Phase.Disabled or Phase.Blocked or Phase.Expired;
            if (pageId is "teleport.tools" or "teleport.utilities") Commands(context, draft, pageId);
            Platform(context, 65, floor, false, 1);
            Platform(context, 244, destinationY, true, blocked ? 0.35 : 1);
            Destination(context, frame.Command, 244, destinationY - 18, blocked ? 0.35 : 1);
            if (pageId == "teleport.homes") Homes(context, draft, floor);
            if (pageId == "teleport.stuckpolicy") Staff(context, frame.Phase == Phase.Blocked, 155, 29);
            if (pageId == "teleport.warmup") Interrupt(context, (int)(Time(time) / 4 % 3), 65, floor - 61, frame.Phase == Phase.Cancelled);
            var arrived = frame.Phase == Phase.Arrived;
            var warp = frame.Phase == Phase.Warp;
            var x = arrived ? 244 : 65;
            var y = arrived ? destinationY : floor;
            if (frame.Phase == Phase.Cancelled) x += 8;
            if (frame.Phase is Phase.Warmup or Phase.Warp)
            {
                Portal(context, 65, floor - 3, frame.Time, warp ? 1 : 0.5);
                if (warp) Portal(context, 244, destinationY - 3, frame.Time + 0.8, 0.8);
            }
            if (arrived) Celebration(context, x, y - 66, frame.Time);
            if (frame.Phase == Phase.Warmup) Badge(context, Math.Ceiling(frame.Remaining).ToString(CultureInfo.InvariantCulture) + " s", 109, height - 24, false);
            if (frame.Gear && !blocked) Gear(context, 144, height - 40, frame.Paid);
            if (frame.Phase is Phase.Disabled or Phase.Blocked or Phase.Expired) Badge(context, frame.Phase == Phase.Disabled ? "Off" : frame.Phase == Phase.Expired ? "Expired" : "Staff online", 106, height - 23, true);
            if (frame.Phase == Phase.Cancelled) Badge(context, "Cancelled", 109, height - 23, true);
        }
        finally { context.Restore(); }
    }

    private static Frame Sample(string pageId, SetupWizardDraft draft, double time)
    {
        string command = pageId switch
        {
            "teleport.tools" => new[] { "tpa", "home", "spawn" }[(int)(time / 8 % 3)],
            "teleport.utilities" => new[] { "top", "back", "stuck" }[(int)(time / 8 % 3)],
            "teleport.requests" or "teleport.requesttiming" => "tpa",
            "teleport.homes" => "home",
            "teleport.stuckpolicy" => "stuck",
            "teleport.warmup" => "home",
            _ => pageId["teleport.".Length..]
        };
        bool gear = command switch
        {
            "tpa" => On(draft, "TpaRequireTemporalGear"),
            "home" or "spawn" => On(draft, "HomeSpawnRequireTemporalGear"),
            "top" => On(draft, "Teleportation.TopRequireTemporalGear"),
            "back" => On(draft, "Teleportation.BackRequireTemporalGear"),
            _ => false
        };
        var enabled = pageId == "teleport.warmup" || Enabled(draft, command);
        if (!enabled) return new(command, Phase.Disabled, "/" + command + " commands are off.", 0, gear, false, time);
        if (pageId == "teleport.stuckpolicy" && !string.IsNullOrWhiteSpace(draft.Get("Teleportation.StuckBlockedByOnlinePrivilege")))
            return new(command, Phase.Blocked, "An admin is online. Please ask for help instead of using /stuck.", 0, false, false, time);
        if (pageId == "teleport.back" && Number(draft, "Teleportation.BackExpiresAfterSeconds") is > 0 and < 30)
            return new(command, Phase.Expired, "Previous location: 30 real seconds ago.<br>Your previous teleport location has expired.", 0, gear, false, time);

        if (pageId == "teleport.requesttiming")
        {
            var timeout = Number(draft, "TpaTimeoutMinutes") * 60;
            var expires = On(draft, "TpaUseTimeout");
            var elapsed = time % Math.Max(8, expires ? timeout + 3 : 12);
            var cooldown = On(draft, "TpaUseCooldown") ? "<br>Next request: " + Format(Number(draft, "TpaCooldownInGameHours")) + " in-game hours." : "<br>No request cooldown.";
            if (expires && elapsed >= timeout)
                return new(command, Phase.Expired, "Teleport request to Mara expired." + (gear ? " Temporal gear returned." : "") + cooldown, 0, gear, false, elapsed);
            return new(command, Phase.Request, "Teleport request has been sent to Mara." +
                (expires ? "<br>Expires in " + Format(Math.Max(0, timeout - elapsed)) + " real seconds." : "<br>This request has no expiry.") + cooldown, 0, gear, gear, elapsed);
        }

        if (pageId == "teleport.warmup")
        {
            var example = (int)(time / 4 % 3);
            var elapsed = time % 4;
            var cancel = example == 0 || On(draft, example == 1 ? "Teleportation.CancelWarmupOnDamage" : "Teleportation.CancelWarmupOnInteraction");
            var cause = example == 0 ? "moved" : example == 1 ? "took damage" : "interacted";
            if (elapsed >= 1 && cancel)
                return new(command, Phase.Cancelled, "Teleport cancelled because you " + cause + ".", 0, false, false, elapsed);
            return new(command, elapsed < 2 ? Phase.Warmup : elapsed < 2.5 ? Phase.Warp : Phase.Arrived,
                elapsed < 2 ? "Stand still for " + Math.Ceiling(2 - elapsed) + " seconds to teleport." : elapsed < 2.5 ? "Teleporting..." : "Teleported to home 'orchard'.", 2 - elapsed, false, false, elapsed, ArrivalTime: 2.5);
        }

        var prefix = "Teleportation." + char.ToUpperInvariant(command[0]) + command[1..];
        var warmup = Number(draft, prefix + "WarmupSeconds");
        var request = command == "tpa" ? 1d : 0d;
        var elapsedTime = time % (warmup + request + 4);
        if (pageId is "teleport.tools" or "teleport.utilities") elapsedTime = time % 8;
        var paid = command == "tpa" && gear;
        if (elapsedTime < request)
            return new(command, Phase.Request, "Teleport request has been sent to Mara." + (gear ? "<br>One temporal gear paid with the request." : ""), 0, gear, paid, elapsedTime);
        var remaining = warmup - (elapsedTime - request);
        if (remaining > 0)
        {
            var text = (command == "tpa" ? "Your teleport request has been accepted!<br>" : "") + "Stand still for " + Math.Ceiling(remaining) + " seconds to teleport.";
            if (pageId == "teleport.homes") text += "<br>Saved homes: " + HomesCount(draft) + " of " + HomesCount(draft) + ".";
            if (gear) text += command == "tpa" ? "<br>Temporal gear already paid." : "<br>One temporal gear will be spent on arrival.";
            if (pageId == "teleport.stuck" && Number(draft, prefix + "ReminderIntervalSeconds") > 0)
                text += "<br>Reminders every " + Format(Number(draft, prefix + "ReminderIntervalSeconds")) + " real seconds.";
            return new(command, Phase.Warmup, text, remaining, gear, paid, elapsedTime);
        }
        var phase = elapsedTime - request - warmup < 0.5 ? Phase.Warp : Phase.Arrived;
        if (phase == Phase.Warp) return new(command, phase, "Teleporting...", 0, gear, gear, elapsedTime);
        var result = command switch
        {
            "tpa" => "Your teleport request has been accepted!<br>Teleported to Mara.",
            "home" => "Teleported to home 'orchard'.",
            "spawn" => "Teleported to spawn.",
            "top" => "Teleported to the surface.",
            "back" => "Teleported back to your previous location.",
            _ => "You have been teleported to spawn."
        };
        var cooldownSeconds = Number(draft, prefix + "CooldownSeconds");
        var cooldownRemaining = Math.Max(0, Math.Ceiling(cooldownSeconds - (elapsedTime - request - warmup)));
        if (command != "tpa") result += cooldownRemaining > 0 ? "<br>Use /" + command + " again in " + Format(cooldownRemaining) + " real seconds." : cooldownSeconds > 0 ? "<br>Ready to use /" + command + " again." : "<br>No cooldown.";
        if (pageId == "teleport.back") result += Number(draft, prefix + "ExpiresAfterSeconds") > 0 ? "<br>Return locations last " + Format(Number(draft, prefix + "ExpiresAfterSeconds")) + " real seconds." : "<br>Return locations do not expire.";
        if (pageId == "teleport.stuckpolicy") result += "<br>Staff receive a /stuck notice.";
        return new(command, phase, result, 0, gear, gear, elapsedTime, ArrivalTime: request + warmup + 0.5);
    }

    private static bool Enabled(SetupWizardDraft draft, string command) => On(draft, command switch
    {
        "tpa" => "AllowPlayerTpa", "home" => "Teleportation.RegisterHomeCommands", "spawn" => "Teleportation.RegisterSpawnCommands",
        "top" => "Teleportation.RegisterTopCommand", "back" => "Teleportation.RegisterBackCommand", _ => "Teleportation.RegisterStuckCommand"
    });

    private static void Commands(Context c, SetupWizardDraft draft, string pageId)
    {
        var commands = pageId == "teleport.tools" ? new[] { "tpa", "home", "spawn" } : new[] { "top", "back", "stuck" };
        for (var i = 0; i < commands.Length; i++)
        {
            c.SetSourceRGBA(0.76, 0.69, 0.53, Enabled(draft, commands[i]) ? 1 : 0.35);
            Label(c, "/" + commands[i], 18 + i * 99, 0, 11);
            c.Arc(78 + i * 99, 7, 2.5, 0, Math.PI * 2); c.Fill();
        }
    }

    private static void Platform(Context c, double x, double y, bool destination, double alpha)
    {
        c.SetSourceRGBA(0, 0, 0, 0.15 * alpha); Ellipse(c, x + 2, y + 23, 47, 9); c.Fill();
        Polygon(c, new[] { x - 43, y, x, y + 19, x, y + 35, x - 43, y + 16 }, 0.27, 0.22, 0.17, alpha);
        Polygon(c, new[] { x, y + 19, x + 43, y, x + 43, y + 16, x, y + 35 }, 0.37, 0.29, 0.20, alpha);
        Polygon(c, new[] { x, y - 19, x + 43, y, x, y + 19, x - 43, y }, destination ? 0.48 : 0.42, 0.48, 0.29, alpha);
        c.SetSourceRGBA(0.75, 0.75, 0.53, 0.3 * alpha); c.LineWidth = 1;
        c.MoveTo(x - 22, y - 9); c.LineTo(x + 21, y + 10); c.MoveTo(x + 22, y - 9); c.LineTo(x - 21, y + 10); c.Stroke();
    }

    private static void Destination(Context c, string command, double x, double y, double alpha)
    {
        c.Save(); c.Translate(x, y); c.SetSourceRGBA(0.82, 0.71, 0.44, alpha); c.LineWidth = 2;
        if (command == "home")
        {
            Polygon(c, new[] { -14d, -8, 0, -21, 14, -8 }, 0.58, 0.32, 0.22, alpha);
            c.SetSourceRGBA(0.76, 0.68, 0.49, alpha); c.Rectangle(-11, -8, 22, 19); c.Fill();
            c.SetSourceRGBA(0.27, 0.21, 0.16, alpha); c.Rectangle(-3, 0, 6, 11); c.Fill();
        }
        else if (command is "spawn" or "stuck")
        {
            c.MoveTo(-5, 10); c.LineTo(-5, -29); c.Stroke();
            Polygon(c, new[] { -4d, -29, 17, -24, -4, -16 }, 0.67, 0.32, 0.22, alpha);
        }
        else if (command == "top")
        {
            c.MoveTo(0, 3); c.LineTo(0, -23); c.MoveTo(-8, -15); c.LineTo(0, -23); c.LineTo(8, -15); c.Stroke();
        }
        else if (command == "back")
        {
            c.SetDash(new[] { 3d, 3d }, 0); Ellipse(c, 0, 4, 16, 7); c.Stroke(); c.SetDash(Array.Empty<double>(), 0);
            c.Arc(0, -14, 10, -Math.PI * 0.3, Math.PI * 1.25); c.Stroke();
            c.MoveTo(-12, -21); c.LineTo(-7, -22); c.LineTo(-8, -16); c.Stroke();
        }
        else
        {
            c.Rectangle(-14, -17, 28, 19); c.Stroke(); c.MoveTo(-14, -17); c.LineTo(0, -5); c.LineTo(14, -17); c.Stroke();
            Label(c, "Mara", -16, -35, 12);
        }
        c.Restore();
    }

    private static void Portal(Context c, double x, double y, double time, double alpha)
    {
        c.SetSourceRGBA(0.25, 0.72, 0.94, alpha); c.LineWidth = 2; Ellipse(c, x, y, 23 + Math.Sin(time * 6) * 3, 8); c.Stroke();
        for (var i = 0; i < 7; i++)
        {
            var angle = i * 2.4 + time * 2; var rise = (time * 19 + i * 13) % 60;
            c.SetSourceRGBA(0.53, 0.88, 1, alpha * (1 - rise / 70)); c.Rectangle(x + Math.Cos(angle) * 22, y - rise, 2, 4); c.Fill();
        }
    }

    private static void Celebration(Context c, double x, double y, double time)
    {
        c.SetSourceRGBA(0.89, 0.77, 0.43, 0.8); c.LineWidth = 2;
        for (var i = 0; i < 3; i++) { var dx = (i - 1) * 16; c.MoveTo(x + dx, y - 6); c.LineTo(x + dx * 1.2, y - 13 - Math.Sin(time * 4 + i) * 2); } c.Stroke();
    }

    private static void Gear(Context c, double x, double y, bool spent)
    {
        c.Save(); c.Translate(x, y); c.SetSourceRGBA(0.32, 0.74, 0.80, spent ? 0.25 : 0.9); c.LineWidth = 3;
        c.Arc(0, 0, 7, 0, Math.PI * 2); c.Stroke();
        for (var i = 0; i < 8; i++) { var a = i * Math.PI / 4; c.MoveTo(Math.Cos(a) * 7, Math.Sin(a) * 7); c.LineTo(Math.Cos(a) * 10, Math.Sin(a) * 10); } c.Stroke(); c.Restore();
    }

    private static void Homes(Context c, SetupWizardDraft draft, double floor)
    {
        var count = HomesCount(draft); c.SetSourceRGBA(0.84, 0.74, 0.47, 0.7);
        for (var i = 0; i < Math.Min(count, 5); i++) { c.Rectangle(212 + i * 12, floor + 25, 7, 4); c.Fill(); }
        if (count > 5) Label(c, "+" + (count - 5), 274, floor + 18, 10);
    }

    private static void Staff(Context c, bool blocked, double x, double y)
    {
        Polygon(c, new[] { x - 10, y - 8, x + 10, y - 8, x + 8, y + 5, x, y + 12, x - 8, y + 5 }, 0.43, 0.47, 0.38, blocked ? 1 : 0.5);
        c.SetSourceRGBA(0.84, 0.76, 0.54, 1); c.MoveTo(x - 4, y); c.LineTo(x - 1, y + 4); c.LineTo(x + 5, y - 4); c.LineWidth = 2; c.Stroke();
    }

    private static void Interrupt(Context c, int example, double x, double y, bool cancelled)
    {
        c.SetSourceRGBA(cancelled ? 0.9 : 0.77, cancelled ? 0.46 : 0.73, 0.4, 1); c.LineWidth = 2;
        if (example == 0) { c.MoveTo(x + 15, y + 14); c.LineTo(x + 28, y + 14); c.LineTo(x + 23, y + 9); c.MoveTo(x + 28, y + 14); c.LineTo(x + 23, y + 19); }
        else if (example == 1) { c.MoveTo(x + 20, y); c.LineTo(x + 15, y + 10); c.LineTo(x + 23, y + 9); c.LineTo(x + 18, y + 20); }
        else { c.Rectangle(x + 17, y + 4, 12, 12); c.MoveTo(x + 12, y + 16); c.LineTo(x + 19, y + 10); }
        c.Stroke();
    }

    private static void Badge(Context c, string text, double x, double y, bool warning)
    {
        c.SetSourceRGBA(warning ? 0.87 : 0.75, warning ? 0.60 : 0.85, warning ? 0.44 : 0.91, 1); Label(c, text, x, y, 12);
    }

    private static void Polygon(Context c, double[] points, double red, double green, double blue, double alpha)
    {
        c.MoveTo(points[0], points[1]); for (var i = 2; i < points.Length; i += 2) c.LineTo(points[i], points[i + 1]); c.ClosePath(); c.SetSourceRGBA(red, green, blue, alpha); c.Fill();
    }
    private static void Ellipse(Context c, double x, double y, double rx, double ry) { c.Save(); c.Translate(x, y); c.Scale(rx, ry); c.Arc(0, 0, 1, 0, Math.PI * 2); c.Restore(); }
    private static void Label(Context c, string text, double x, double y, double size)
    {
        c.SelectFontFace(GuiStyle.StandardFontName, FontSlant.Normal, FontWeight.Normal); c.SetFontSize(size); c.MoveTo(x, y + c.FontExtents.Ascent); c.ShowText(text);
    }
    private static int HomesCount(SetupWizardDraft draft) => Number(draft, "Teleportation.MaxHomes") > 0 ? (int)Number(draft, "Teleportation.MaxHomes") : 3;
    private static bool On(SetupWizardDraft draft, string key) => ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out var on) && on;
    private static double Number(SetupWizardDraft draft, string key) => double.TryParse(draft.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? Math.Clamp(value, 0, 86400) : 0;
    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static double Time(double time) => double.IsFinite(time) ? Math.Max(0, time) : 0;
}
