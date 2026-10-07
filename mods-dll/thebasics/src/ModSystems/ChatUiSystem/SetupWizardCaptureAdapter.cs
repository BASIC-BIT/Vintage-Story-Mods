using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Optional QA operations registered with Agent Control, never a second input controller.</summary>
public sealed class SetupWizardCaptureAdapter : IRenderer
{
    private sealed class Job
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Scenario;
        public double PreviewTime;
        public SetupWizardDialog Dialog;
        public long QueuedAt;
        public long? ReadyFrame;
        public string Status = "queued";
        public string Directory;
        public string Error;
        public CancellationToken Cancellation;
    }

    private readonly ICoreClientAPI api;
    private readonly Action requestAuthorizedOpen;
    private readonly System.Func<SetupWizardDialog> currentDialog;
    private readonly List<IDisposable> registrations = new();
    private readonly Dictionary<string, Job> jobs = new(StringComparer.Ordinal);
    private readonly string captureRoot = Path.GetFullPath(Path.Combine(GamePaths.DataPath, "Screenshots", "thebasics-setup"));
    private readonly long tick;
    private Job pending;
    private SetupWizardDialog ownedDialog;
    private bool openedForCapture, disposed, registryFailed;
    private long frame;
    public double RenderOrder => 3;
    public int RenderRange => int.MaxValue;

    public SetupWizardCaptureAdapter(ICoreClientAPI api, Action requestAuthorizedOpen, System.Func<SetupWizardDialog> currentDialog)
    {
        this.api = api;
        this.requestAuthorizedOpen = requestAuthorizedOpen;
        this.currentDialog = currentDialog;
        tick = api.Event.RegisterGameTickListener(OnTick, 250);
        api.Event.RegisterRenderer(this, EnumRenderStage.Done, "thebasics-setup-capture");
    }

    private void OnTick(float dt)
    {
        if (disposed) return;
        if (pending != null && api.ElapsedMilliseconds - pending.QueuedAt > 30000) Fail(pending, "Preview did not become ready within 30 seconds.");
        if (registrations.Count != 0 || registryFailed || !api.ObjectCache.TryGetValue("agentcontrol:registry", out var registry)) return;
        try
        {
            Register(registry, "thebasics.setup.open", "Open the authorized capture-only setup dialog.", true, Open);
            Register(registry, "thebasics.setup.capture", "Queue one named native capture after GUI readiness; poll the returned job ID.", true, Queue);
            Register(registry, "thebasics.setup.poll", "Read the bounded capture job result.", false, Poll);
        }
        catch (Exception error)
        {
            foreach (var registration in registrations) registration.Dispose();
            registrations.Clear();
            registryFailed = true;
            api.Logger.Warning("The BASICs optional capture extension unavailable: {0}", error.GetBaseException().Message);
        }
    }

    private void Register(object registry, string operation, string description, bool mutates, System.Func<JsonElement, CancellationToken, JsonElement> handler)
    {
        // Bind the installed public contract without adding an Agent Control build/runtime dependency.
        var method = registry.GetType().GetMethod("Register", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException("Agent Control registry contract changed.");
        var parameters = method.GetParameters();
        var descriptor = Activator.CreateInstance(parameters[0].ParameterType, "thebasics.setup", "1", operation, description, mutates);
        var delegateType = parameters[1].ParameterType;
        var invoke = delegateType.GetMethod("Invoke");
        var context = Expression.Parameter(invoke.GetParameters()[0].ParameterType, "context");
        var arguments = Expression.Parameter(typeof(JsonElement), "arguments");
        var body = Expression.Invoke(Expression.Constant(handler), arguments, Expression.Property(context, "CancellationToken"));
        var callback = Expression.Lambda(delegateType, body, context, arguments).Compile();
        registrations.Add((IDisposable)method.Invoke(registry, new[] { descriptor, callback }));
    }

    private void RequireCurrentUser()
    {
        if (!api.PlayerReadyFired || api.World.Player == null || !api.World.Player.HasPrivilege(Privilege.root))
            throw new UnauthorizedAccessException("A ready, currently authorized administrator is required for setup captures.");
    }

    private JsonElement Open(JsonElement arguments, CancellationToken cancellation)
    {
        RequireCurrentUser();
        cancellation.ThrowIfCancellationRequested();
        CheckArguments(arguments);
        if (pending != null) throw new InvalidOperationException("Wait for the queued capture before reopening.");
        if (jobs.Count >= 32) throw new InvalidOperationException("Capture session limit reached; reconnect to begin another session.");
        var existing = currentDialog();
        if (existing != null && !ReferenceEquals(existing, ownedDialog)) throw new InvalidOperationException("Close the existing wizard before beginning a capture.");
        ownedDialog?.TryClose();
        ownedDialog?.Dispose();
        ownedDialog = null;
        requestAuthorizedOpen();
        openedForCapture = true;
        return JsonSerializer.SerializeToElement(new { status = "opening", scenes = SetupWizardCaptureScenes.Names, captureRoot });
    }

    private JsonElement Queue(JsonElement arguments, CancellationToken cancellation)
    {
        RequireCurrentUser();
        cancellation.ThrowIfCancellationRequested();
        CheckArguments(arguments, "scenario", "previewTime");
        var scenario = arguments.GetProperty("scenario").GetString();
        var previewTime = arguments.TryGetProperty("previewTime", out var time) ? time.GetDouble() : 0.75;
        SetupWizardCaptureScenes.Validate(scenario, previewTime);
        if (pending != null) throw new InvalidOperationException("A capture is already queued.");
        if (!openedForCapture) throw new InvalidOperationException("Open a fresh capture-only wizard before each scene.");
        if (jobs.Count >= 32) throw new InvalidOperationException("Capture session limit reached.");
        var dialog = currentDialog() ?? throw new InvalidOperationException("Wait for the server-authorized capture dialog to open.");
        if (dialog.LayoutOnly || !dialog.IsCaptureOnly) throw new InvalidOperationException("Native captures require the native capture-only production preview.");
        ownedDialog = dialog;
        SetupWizardCaptureScenes.Show(dialog, scenario, previewTime);
        var job = new Job { Scenario = scenario, PreviewTime = previewTime, Dialog = dialog, QueuedAt = api.ElapsedMilliseconds, Cancellation = cancellation };
        jobs.Add(job.Id, job);
        pending = job;
        openedForCapture = false;
        return Result(job);
    }

    private JsonElement Poll(JsonElement arguments, CancellationToken cancellation)
    {
        RequireCurrentUser();
        cancellation.ThrowIfCancellationRequested();
        CheckArguments(arguments, "jobId");
        var id = arguments.GetProperty("jobId").GetString();
        if (!Guid.TryParseExact(id, "N", out _) || !jobs.TryGetValue(id, out var job)) throw new ArgumentException("Unknown capture job.");
        return Result(job);
    }

    private static JsonElement Result(Job job) => JsonSerializer.SerializeToElement(new
    { jobId = job.Id, status = job.Status, scenario = job.Scenario, previewTime = job.PreviewTime, directory = job.Directory, error = job.Error });

    private static void CheckArguments(JsonElement arguments, params string[] keys)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null && keys.Length == 0) return;
        if (arguments.ValueKind != JsonValueKind.Object) throw new ArgumentException("Capture arguments must be an object.");
        foreach (var property in arguments.EnumerateObject())
            if (!keys.Contains(property.Name, StringComparer.Ordinal)) throw new ArgumentException("Unknown capture argument: " + property.Name);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        frame++;
        var job = pending;
        if (disposed || job == null || stage != EnumRenderStage.Done) return;
        try
        {
            RequireCurrentUser();
            job.Cancellation.ThrowIfCancellationRequested();
            if (!ReferenceEquals(currentDialog(), job.Dialog) || !job.Dialog.IsOpened()) throw new InvalidOperationException("Capture dialog was closed or replaced.");
            if (!job.Dialog.IsPreviewReady) return;
            if (job.ReadyFrame == null) { job.ReadyFrame = frame; return; }
            if (frame <= job.ReadyFrame) return;
            Capture(job);
            job.Dialog.ResumePreview();
            job.Status = "completed";
            pending = null;
        }
        catch (Exception error) { Fail(job, error.GetBaseException().Message); }
    }

    private void Capture(Job job)
    {
        var game = api.World as ClientMain ?? throw new NotSupportedException("Native framebuffer capture requires the installed ClientMain.");
        // The engine's transformed screenshot path leaks its original bitmap. Own the raw grab and our flip.
        using var raw = game.Platform.GrabScreenshot(withAlpha: false, scale: false);
        if (raw.Width is <= 0 or > 4096 || raw.Height is <= 0 or > 4096) throw new InvalidDataException("Capture dimensions must be 1..4096 pixels per axis.");
        if (raw is not BitmapExternal external) throw new NotSupportedException("Screenshot bitmap implementation changed.");
        var directory = Path.GetFullPath(Path.Combine(captureRoot, job.Id));
        if (!directory.StartsWith(captureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Capture escaped its fixed directory.");
        Directory.CreateDirectory(directory);
        using var flipped = new SKBitmap(raw.Width, raw.Height);
        using (var canvas = new SKCanvas(flipped))
        {
            canvas.Translate(0, raw.Height);
            canvas.Scale(1, -1);
            canvas.DrawBitmap(external.bmp, 0, 0);
        }
        using (var encoded = flipped.Encode(SKEncodedImageFormat.Png, 100))
        using (var output = File.Create(Path.Combine(directory, job.Scenario + ".png"))) encoded.SaveTo(output);
        var sourceTreeHash = typeof(SetupWizardDialog).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "GuiCaptureSourceTreeHash")?.Value;
        var gamePath = Path.GetDirectoryName(typeof(ClientMain).Assembly.Location);
        var manifest = new
        {
            schema = 1, scenario = job.Scenario, width = raw.Width, height = raw.Height, scale = (double)RuntimeEnv.GUIScale,
            previewTime = job.PreviewTime, fixture = job.Dialog.Draft.Values, coverage = "native-client", omissions = Array.Empty<string>(), sourceTreeHash,
            environmentIdentity = SetupWizardCaptureIdentity.EnvironmentIdentity(gamePath, raw.Width, raw.Height, RuntimeEnv.GUIScale,
                Lang.CurrentLocale ?? "en", GuiStyle.StandardFontName, GuiStyle.DecorativeFontName, api.Assets),
            environment = new { gameVersion = GameVersion.ShortGameVersion, modBinaryHash = SetupWizardCaptureIdentity.HashFile(typeof(SetupWizardDialog).Assembly.Location),
                runId = job.Dialog.RunId, renderStage = "Done", readyFrame = job.ReadyFrame, captureFrame = frame,
                sourceIdentityVerified = sourceTreeHash != null, fidelity = "Native production dialog in the current client framebuffer. Human gameplay acceptance is separate." }
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(directory, job.Scenario + ".json"), JsonSerializer.Serialize(manifest, options));
        File.WriteAllText(Path.Combine(directory, "captures.json"), JsonSerializer.Serialize(new
        { schema = 1, sourceTreeHash, coverage = "native-client", captures = new[] { new { scenario = job.Scenario, file = job.Scenario + ".png", manifest = job.Scenario + ".json" } } }, options));
        job.Directory = directory;
    }

    private void Fail(Job job, string error) { job.Status = "failed"; job.Error = error; if (ReferenceEquals(pending, job)) pending = null; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (pending != null) Fail(pending, "Capture adapter was disposed.");
        foreach (var registration in registrations) registration.Dispose();
        api.Event.UnregisterGameTickListener(tick);
        api.Event.UnregisterRenderer(this, EnumRenderStage.Done);
        ownedDialog?.TryClose();
        ownedDialog?.Dispose();
        jobs.Clear();
    }
}
