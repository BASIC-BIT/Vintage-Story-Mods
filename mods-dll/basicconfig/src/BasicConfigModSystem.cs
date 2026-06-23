using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace BasicConfig;

public sealed class BasicConfigModSystem : ModSystem
{
    public const string ModId = "basicconfig";

    private readonly Dictionary<string, IBasicConfigServerController> _serverControllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BasicConfigClientController> _clientControllers = new(StringComparer.OrdinalIgnoreCase);
    private ICoreServerAPI _serverApi;
    private ICoreClientAPI _clientApi;
    private IServerNetworkChannel _serverChannel;
    private IClientNetworkChannel _clientChannel;

    public override double ExecuteOrder()
    {
        return 0.05;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;
        _serverChannel = api.Network.RegisterChannel(ModId)
            .RegisterMessageType<BasicConfigOpenMessage>()
            .RegisterMessageType<BasicConfigSaveMessage>()
            .RegisterMessageType<BasicConfigResultMessage>()
            .SetMessageHandler<BasicConfigSaveMessage>(OnSaveMessage);

        RegisterCommand(api);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientChannel = api.Network.RegisterChannel(ModId)
            .RegisterMessageType<BasicConfigOpenMessage>()
            .RegisterMessageType<BasicConfigSaveMessage>()
            .RegisterMessageType<BasicConfigResultMessage>()
            .SetMessageHandler<BasicConfigOpenMessage>(OnOpenMessage)
            .SetMessageHandler<BasicConfigResultMessage>(OnResultMessage);
    }

    public BasicConfigServerController<TConfig> RegisterServer<TConfig>(BasicConfigServerControllerOptions<TConfig> options) where TConfig : class, new()
    {
        if (_serverChannel == null)
        {
            throw new InvalidOperationException("BasicConfig server channel is not ready.");
        }

        options = options ?? throw new ArgumentNullException(nameof(options));
        options.Channel = _serverChannel;
        var controller = new BasicConfigServerController<TConfig>(options);
        _serverControllers[controller.ConfigId] = controller;
        return controller;
    }

    public BasicConfigClientController RegisterClient(BasicConfigClientOptions options)
    {
        if (_clientChannel == null)
        {
            throw new InvalidOperationException("BasicConfig client channel is not ready.");
        }

        options = options ?? throw new ArgumentNullException(nameof(options));
        options.Api ??= _clientApi;
        options.SendPacket = packet =>
        {
            if (packet is BasicConfigSaveMessage saveMessage && _clientChannel.Connected)
            {
                _clientChannel.SendPacket(saveMessage);
            }
        };

        var controller = new BasicConfigClientController(options);
        _clientControllers[options.ConfigId] = controller;
        return controller;
    }

    public bool SendOpen(IServerPlayer player, string configId, string statusMessage = null)
    {
        if (player == null || string.IsNullOrWhiteSpace(configId) || !_serverControllers.TryGetValue(configId, out var controller))
        {
            return false;
        }

        controller.SendOpen(player, statusMessage);
        return true;
    }

    private void RegisterCommand(ICoreServerAPI api)
    {
        api.ChatCommands.Create(ModId)
            .WithDescription("Open registered BasicConfig config panels")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .WithArgs(new StringArgParser("configId", false))
            .HandleWith(HandleBasicConfig)
            .BeginSubCommand("list")
                .WithDescription("List config panels available through BasicConfig")
                .HandleWith(HandleBasicConfigList)
            .EndSubCommand()
            .BeginSubCommand("open")
                .WithDescription("Open a registered BasicConfig config panel")
                .WithArgs(new StringArgParser("configId", true))
                .HandleWith(HandleBasicConfigOpen)
            .EndSubCommand();
    }

    private TextCommandResult HandleBasicConfig(TextCommandCallingArgs args)
    {
        var configId = ((string)args[0] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configId))
        {
            return HandleBasicConfigList(args);
        }

        return OpenConfig(args.Caller.Player as IServerPlayer, configId);
    }

    private TextCommandResult HandleBasicConfigList(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        var available = _serverControllers.Values
            .Where(controller => controller.CanEdit(player))
            .OrderBy(controller => controller.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (available.Count == 0)
        {
            return TextCommandResult.Success("No BasicConfig panels are available to you.");
        }

        var lines = available.Select(controller => $"/basicconfig {controller.ConfigId} - {controller.DisplayName}");
        return TextCommandResult.Success("Available BasicConfig panels:\n" + string.Join("\n", lines));
    }

    private TextCommandResult HandleBasicConfigOpen(TextCommandCallingArgs args)
    {
        return OpenConfig(args.Caller.Player as IServerPlayer, ((string)args[0] ?? string.Empty).Trim());
    }

    private TextCommandResult OpenConfig(IServerPlayer player, string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
        {
            return TextCommandResult.Error("Usage: /basicconfig <configId>");
        }

        if (!_serverControllers.TryGetValue(configId, out var controller))
        {
            return TextCommandResult.Error($"Unknown BasicConfig panel '{configId}'. Use /basicconfig list.");
        }

        if (!controller.CanEdit(player))
        {
            return TextCommandResult.Error($"You do not have permission to edit {controller.DisplayName} config.");
        }

        controller.SendOpen(player);
        return TextCommandResult.Success($"Opening {controller.DisplayName} config panel.");
    }

    private void OnSaveMessage(IServerPlayer player, BasicConfigSaveMessage message)
    {
        if (string.IsNullOrWhiteSpace(message?.ConfigId) || !_serverControllers.TryGetValue(message.ConfigId, out var controller))
        {
            return;
        }

        controller.OnSaveMessage(player, message);
    }

    private void OnOpenMessage(BasicConfigOpenMessage message)
    {
        if (string.IsNullOrWhiteSpace(message?.ConfigId) || !_clientControllers.TryGetValue(message.ConfigId, out var controller))
        {
            _clientApi?.ShowChatMessage($"Received BasicConfig panel for unknown config '{message?.ConfigId}'.");
            return;
        }

        controller.OnOpenMessage(message);
    }

    private void OnResultMessage(BasicConfigResultMessage message)
    {
        if (string.IsNullOrWhiteSpace(message?.ConfigId) || !_clientControllers.TryGetValue(message.ConfigId, out var controller))
        {
            _clientApi?.ShowChatMessage($"Received BasicConfig result for unknown config '{message?.ConfigId}'.");
            return;
        }

        controller.OnResultMessage(message);
    }
}

public interface IBasicConfigServerController
{
    string ConfigId { get; }
    string DisplayName { get; }
    bool CanEdit(IServerPlayer player);
    void SendOpen(IServerPlayer player, string statusMessage = null);
    void OnSaveMessage(IServerPlayer player, BasicConfigSaveMessage message);
}
