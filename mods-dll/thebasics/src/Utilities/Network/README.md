# Safe Network Channel Utilities

This directory contains utilities for safe client-side network communication in Vintage Story mods.

## SafeClientNetworkChannel

The `SafeClientNetworkChannel` class provides a robust wrapper around `IClientNetworkChannel` that handles connection timing issues, retry mechanisms, and packet queuing to prevent "Attempting to send data to a not connected channel" errors.

### Problem Solved

When clients connect to a server, there's a timing window where:
1. The network channel is registered and exists
2. But the channel is not yet "connected" (handshake incomplete)
3. Attempting to send packets during this window causes errors

This utility solves this by:
- Checking connection status before sending packets
- Queuing packets when channel is not connected
- Implementing automatic retry mechanism with configurable delays
- Processing queued packets when connection is established

### Usage Example

```csharp
using thebasics.Utilities.Network;

public class MyClientModSystem : ModSystem
{
    private IClientNetworkChannel _channel;
    private SafeClientNetworkChannel _safeChannel;
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        // Register your channel normally
        _channel = api.Network.RegisterChannel("mychannel")
            .RegisterMessageType<MyMessage>()
            .SetMessageHandler<MyMessage>(OnMessage);
            
        // Wrap it with the safe channel utility
        var config = new SafeClientNetworkChannel.SafeNetworkChannelConfig
        {
            LogPrefix = "[MYMOD]",
            EnableDebugLogging = true,
            RetryDelayMs = 2000,    // 2 second retry delay
            MaxRetries = 10         // Maximum 10 retry attempts
        };
        _safeChannel = new SafeClientNetworkChannel(_channel, api, config);
        
        // Register event handlers
        api.Event.PlayerJoin += OnPlayerJoin;
    }
    
    private void OnPlayerJoin(IClientPlayer player)
    {
        // Safe packet sending - will queue and retry if channel not connected
        _safeChannel.SendPacketSafely(new MyMessage { Data = "Hello Server!" });
    }
    
    public override void Dispose()
    {
        _safeChannel?.Dispose();
        _safeChannel = null;
    }
}
```

### Configuration Options

- **RetryDelayMs**: Delay between connection retry attempts (default: 2000ms)
- **MaxRetries**: Maximum retry attempts before giving up (default: 10)
- **EnableDebugLogging**: Whether to log debug information (default: true)
- **LogPrefix**: Prefix for log messages to identify the source (default: "[SAFE_NETWORK]")

### Key Methods

- **SendPacketSafely<T>(T message)**: Safely sends a packet with automatic retry
- **QueuePacketAction(Action action)**: Queues a custom action for when channel is connected
- **IsConnected**: Property to check if the underlying channel is connected
- **PendingActionCount**: Number of actions waiting in the queue
- **ClearPendingActions()**: Manually clear the queue (useful for cleanup)
- **ResetRetryState()**: Reset retry counters (useful for reconnection scenarios)

### When to Use

Use `SafeClientNetworkChannel` for:
- ✅ Client-side mod systems that send packets to the server
- ✅ Packets sent during player join events
- ✅ Any client-initiated network communication

Don't use for:
- ❌ Server-side mod systems (servers don't have connection timing issues)
- ❌ Simple message handlers that only receive packets
- ❌ One-time setup code that doesn't send packets

### Integration with Existing Code

To migrate existing client-side networking code:

1. **Before** (unsafe):
```csharp
_channel.SendPacket(new MyMessage());
```

2. **After** (safe):
```csharp
_safeChannel.SendPacketSafely(new MyMessage());
```

The utility is designed as a drop-in replacement that enhances reliability without changing the core networking logic.

### Error Handling

The utility handles several error scenarios:
- **Channel not connected**: Queues packet and retries automatically
- **Maximum retries exceeded**: Logs warning and clears queue to prevent memory buildup
- **Action execution errors**: Logs errors but continues processing other queued actions
- **Disposal**: Properly cleans up resources and pending actions

### Performance Considerations

- **Memory**: Queued actions are cleared after max retries to prevent memory leaks
- **CPU**: Retry mechanism uses game's callback system, no busy waiting
- **Network**: No additional network overhead, just timing management
- **Logging**: Debug logging can be disabled for production use

### Thread Safety

The utility is designed for single-threaded use within Vintage Story's mod system. All operations should be called from the main game thread.