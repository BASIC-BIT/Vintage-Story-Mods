using System.Text.Json;
using System.Security.Cryptography;
using AgentControl.Abstractions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace AgentControl;

public sealed class AgentControlModSystem : ModSystem
{
    private const string ToggleHotkey = "agentcontrol-toggle";
    private const string KillHotkey = "agentcontrol-kill";
    private ICoreClientAPI? _api;
    private AgentControlConfig _config = new();
    private ExtensionRegistry? _extensions;
    private VintageStoryAgentGame? _game;
    private ExecutionEngine? _engine;
    private GameThreadDispatcher? _dispatcher;
    private AgentControlHud? _hud;
    private NamedPipeRpcServer? _server;
    private string? _sessionSecret;
    private long _tickListener;
    private bool _enabled;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        _api = api;
        _config = api.LoadModConfig<AgentControlConfig>("agentcontrol.json") ?? new AgentControlConfig();
        api.StoreModConfig(_config, "agentcontrol.json");
        _extensions = new ExtensionRegistry();
        api.ObjectCache[AgentControlContract.RegistryObjectCacheKey] = _extensions;
        _game = new VintageStoryAgentGame(api, _extensions, _config);
        _engine = new ExecutionEngine(_game, new StopwatchClock(), _config);
        _dispatcher = new GameThreadDispatcher();
        _hud = new AgentControlHud(api);

        api.Input.RegisterHotKey(ToggleHotkey, "Toggle Agent Control", GlKeys.F8, HotkeyType.HelpAndOverlays, altPressed: true, ctrlPressed: true);
        api.Input.SetHotKeyHandler(ToggleHotkey, _ => { Toggle(); return true; });
        api.Input.RegisterHotKey(KillHotkey, "Kill Agent Control", GlKeys.F9, HotkeyType.HelpAndOverlays, altPressed: true, ctrlPressed: true);
        api.Input.SetHotKeyHandler(KillHotkey, _ => { Kill(); return true; });
        api.Event.ChatMessage += OnChatMessage;
        api.Event.LeaveWorld += OnLeaveWorld;
        _tickListener = api.Event.RegisterGameTickListener(OnTick, 20);
        _hud.SetState(false, false, _config.GrantMutationOnEnable);
    }

    private void OnTick(float deltaTime)
    {
        _dispatcher?.Drain();
        _engine?.Tick();
        _hud?.SetState(_enabled, _engine?.IsActive == true, _config.GrantMutationOnEnable);
    }

    private void OnChatMessage(int groupId, string message, EnumChatType chatType, string data) =>
        _game?.RecordChat(groupId, message, chatType);

    private void OnLeaveWorld() => Kill();

    private void Toggle()
    {
        if (_enabled)
        {
            Disable("toggle");
        }
        else
        {
            Enable();
        }
    }

    private void Enable()
    {
        if (_enabled || _api is null)
        {
            return;
        }
        _sessionSecret = NamedPipeRpcServer.CreateSessionSecret();
        _server = new NamedPipeRpcServer(_config.PipeName, _config.MaxRequestBytes, HandleRequest, () =>
        {
            Observe(_dispatcher?.Invoke(() =>
            {
                Kill();
                return true;
            }));
        }, OnServerFailed);
        _server.Start();
        _enabled = true;
        _game?.Audit("session.enabled", new { pipe = _config.PipeName, mutationGranted = _config.GrantMutationOnEnable });
        _api.ShowChatMessage("Agent Control enabled. Ctrl+Alt+F9 cancels and releases input.");
    }

    private void OnServerFailed(Exception error)
    {
        _api?.Logger.Error("Agent Control pipe failed: {0}", error);
        Observe(_dispatcher?.Invoke(() =>
        {
            Disable("pipe_failed");
            return true;
        }));
    }

    private static void Observe(Task? task) =>
        task?.ContinueWith(
            t => _ = t.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

    private void Kill()
    {
        _engine?.CancelAll();
        _game?.ReleaseOwnedControls();
        _game?.Audit("kill_switch", new { active = _engine?.IsActive == true });
    }

    private void Disable(string reason)
    {
        if (!_enabled)
        {
            return;
        }
        Kill();
        _enabled = false;
        _sessionSecret = null;
        if (reason == "pipe_failed")
        {
            _api?.ShowChatMessage("Agent Control disabled: named pipe failed, see client log.");
        }
        var server = Interlocked.Exchange(ref _server, null);
        if (server is not null)
        {
            _ = server.DisposeAsync();
        }
        _game?.Audit("session.disabled", new { reason });
    }

    private async Task<RpcResponse> HandleRequest(RpcRequest request)
    {
        try
        {
            if (!Protocol.Methods.Contains(request.Method))
            {
                return Error(request.Id, "method_not_found", $"Unknown RPC method '{request.Method}'.");
            }

            if (request.Method == "hello")
            {
                return new RpcResponse(request.Id, true, new
                {
                    protocolVersion = AgentControlContract.ProtocolVersion,
                    modVersion = "0.1.0",
                    gameVersion = GameVersion.ShortGameVersion,
                    session = _sessionSecret,
                    mutationGranted = _config.GrantMutationOnEnable,
                    methods = Protocol.Methods.Order(StringComparer.Ordinal).ToArray()
                });
            }

            if (string.IsNullOrEmpty(_sessionSecret) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(request.Session ?? string.Empty),
                    System.Text.Encoding.UTF8.GetBytes(_sessionSecret)))
            {
                return Error(request.Id, "unauthorized", "A valid session secret is required.");
            }

            return request.Method switch
            {
                "observe" => new RpcResponse(request.Id, true, await _dispatcher!.Invoke(() => _game!.Observe()).ConfigureAwait(false)),
                "extensions.list" => new RpcResponse(request.Id, true, await _dispatcher!.Invoke(() => _extensions!.List()).ConfigureAwait(false)),
                "execute" => await Execute(request).ConfigureAwait(false),
                "cancel" => new RpcResponse(request.Id, true, new
                {
                    cancelled = await _dispatcher!.Invoke(() => _engine!.Cancel(ReadExecutionId(request.Params))).ConfigureAwait(false)
                }),
                "shutdownSession" => await Shutdown(request.Id).ConfigureAwait(false),
                _ => Error(request.Id, "method_not_found", "Unknown method.")
            };
        }
        catch (Exception ex)
        {
            return Error(request.Id, "request_failed", ex.Message);
        }
    }

    private async Task<RpcResponse> Execute(RpcRequest request)
    {
        var parameters = request.Params ?? throw new InvalidOperationException("execute params are required.");
        var executionId = parameters.TryGetProperty("executionId", out var id)
            ? id.GetString() ?? Guid.NewGuid().ToString("n")
            : Guid.NewGuid().ToString("n");
        var actions = parameters.GetProperty("actions").EnumerateArray().Select(action => action.Clone()).ToArray();
        if (!_config.GrantMutationOnEnable && await _dispatcher!.Invoke(() => actions.Any(IsMutatingAction)).ConfigureAwait(false))
        {
            return Error(request.Id, "mutation_denied", "This session has no mutation grant.");
        }
        var task = await _dispatcher!.Invoke(() => _engine!.Enqueue(executionId, actions)).ConfigureAwait(false);
        var receipt = await task.ConfigureAwait(false);
        return new RpcResponse(request.Id, true, receipt);
    }

    private bool IsMutatingAction(JsonElement action)
    {
        var type = action.GetProperty("type").GetString();
        if (type is "wait" or "wait_for")
        {
            return false;
        }
        if (type != "extension.invoke")
        {
            return true;
        }
        var operation = action.GetProperty("operation").GetString();
        return _extensions!.List().SingleOrDefault(item => item.Operation == operation)?.MutatesState ?? true;
    }

    private async Task<RpcResponse> Shutdown(string requestId)
    {
        await _dispatcher!.Invoke(() =>
        {
            Disable("rpc");
            return true;
        }).ConfigureAwait(false);
        return new RpcResponse(requestId, true, new { shutdown = true });
    }

    private static string? ReadExecutionId(JsonElement? parameters) =>
        parameters is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty("executionId", out var id)
            ? id.GetString()
            : null;

    private static RpcResponse Error(string id, string code, string message) =>
        new(id, false, Error: new RpcError(code, message));

    public override void Dispose()
    {
        Disable("dispose");
        _engine?.Dispose();
        _game?.ReleaseOwnedControls();
        if (_api is not null)
        {
            _api.Event.ChatMessage -= OnChatMessage;
            _api.Event.LeaveWorld -= OnLeaveWorld;
            if (_tickListener != 0)
            {
                _api.Event.UnregisterGameTickListener(_tickListener);
            }
            _dispatcher?.Shutdown();
            if (_api.ObjectCache.TryGetValue(AgentControlContract.RegistryObjectCacheKey, out var current) &&
                ReferenceEquals(current, _extensions))
            {
                _api.ObjectCache.Remove(AgentControlContract.RegistryObjectCacheKey);
            }
        }
        _hud?.Dispose();
        base.Dispose();
    }
}
