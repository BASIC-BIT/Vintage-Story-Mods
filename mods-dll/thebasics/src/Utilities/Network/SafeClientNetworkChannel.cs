using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Utilities.Network
{
    /// <summary>
    /// A utility class that provides safe packet sending for client-side network channels.
    /// Handles connection checking, retry mechanisms, and queue-based packet handling
    /// to prevent "Attempting to send data to a not connected channel" errors.
    /// </summary>
    public class SafeClientNetworkChannel
    {
        private readonly IClientNetworkChannel _channel;
        private readonly ICoreClientAPI _api;
        private readonly Queue<Action> _pendingPacketActions;
        private readonly SafeNetworkChannelConfig _config;
        
        private bool _connectionRetryInProgress;
        private int _connectionRetryCount;

        /// <summary>
        /// Configuration for the safe network channel behavior
        /// </summary>
        public class SafeNetworkChannelConfig
        {
            /// <summary>
            /// Delay between connection retry attempts in milliseconds
            /// </summary>
            public int RetryDelayMs { get; set; } = 2000;
            
            /// <summary>
            /// Maximum number of connection retry attempts before giving up
            /// </summary>
            public int MaxRetries { get; set; } = 10;
            
            /// <summary>
            /// Whether to log debug information about packet sending and retries
            /// </summary>
            public bool EnableDebugLogging { get; set; } = true;
            
            /// <summary>
            /// Prefix for log messages to identify the source
            /// </summary>
            public string LogPrefix { get; set; } = "[SAFE_NETWORK]";
        }

        /// <summary>
        /// Creates a new SafeClientNetworkChannel wrapper
        /// </summary>
        /// <param name="channel">The client network channel to wrap</param>
        /// <param name="api">The client API for logging and callbacks</param>
        /// <param name="config">Configuration for retry behavior (optional)</param>
        public SafeClientNetworkChannel(IClientNetworkChannel channel, ICoreClientAPI api, SafeNetworkChannelConfig config = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _config = config ?? new SafeNetworkChannelConfig();
            _pendingPacketActions = new Queue<Action>();
            _connectionRetryInProgress = false;
            _connectionRetryCount = 0;
        }

        /// <summary>
        /// Gets whether the underlying channel is connected
        /// </summary>
        public bool IsConnected => _channel?.Connected == true;

        /// <summary>
        /// Gets the number of pending packet actions in the queue
        /// </summary>
        public int PendingActionCount => _pendingPacketActions.Count;

        /// <summary>
        /// Gets the current retry count
        /// </summary>
        public int RetryCount => _connectionRetryCount;

        /// <summary>
        /// Safely sends a packet, handling connection checking and retry logic
        /// </summary>
        /// <typeparam name="T">The type of message to send</typeparam>
        /// <param name="message">The message to send</param>
        public void SendPacketSafely<T>(T message)
        {
            QueuePacketAction(() =>
            {
                if (IsConnected)
                {
                    _channel.SendPacket(message);
                    if (_config.EnableDebugLogging)
                    {
                        _api.Logger.Debug($"{_config.LogPrefix} Successfully sent packet: {typeof(T).Name}");
                    }
                }
                else
                {
                    _api.Logger.Warning($"{_config.LogPrefix} Cannot send packet {typeof(T).Name} - channel not connected");
                }
            });
        }

        /// <summary>
        /// Executes an action immediately if connected, or queues it for retry if not
        /// </summary>
        /// <param name="action">The action to execute</param>
        public void QueuePacketAction(Action action)
        {
            // Always queue the action first to ensure consistent behavior
            _pendingPacketActions.Enqueue(action);
            
            if (IsConnected)
            {
                // Process the queue immediately if connected
                ProcessPacketActionQueue();
            }
            else
            {
                if (_config.EnableDebugLogging)
                {
                    _api.Logger.Debug($"{_config.LogPrefix} Packet action queued until channel is connected");
                }
                
                // Start retry mechanism if not already in progress
                if (!_connectionRetryInProgress)
                {
                    StartConnectionRetry();
                }
            }
        }

        /// <summary>
        /// Processes all queued packet actions
        /// </summary>
        public void ProcessPacketActionQueue()
        {
            if (_config.EnableDebugLogging)
            {
                _api.Logger.Debug($"{_config.LogPrefix} Processing {_pendingPacketActions.Count} queued packet actions");
            }
            
            while (_pendingPacketActions.Count > 0)
            {
                var action = _pendingPacketActions.Dequeue();
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    _api.Logger.Error($"{_config.LogPrefix} Error executing queued packet action: {e}");
                }
            }
        }

        /// <summary>
        /// Starts the connection retry mechanism
        /// </summary>
        private void StartConnectionRetry()
        {
            if (_connectionRetryInProgress || _connectionRetryCount >= _config.MaxRetries)
            {
                if (_connectionRetryCount >= _config.MaxRetries)
                {
                    _api.Logger.Warning($"{_config.LogPrefix} Maximum connection retries ({_config.MaxRetries}) reached, giving up");
                    ClearPendingActions();
                }
                return;
            }

            _connectionRetryInProgress = true;
            _connectionRetryCount++;
            
            if (_config.EnableDebugLogging)
            {
                _api.Logger.Debug($"{_config.LogPrefix} Starting connection retry {_connectionRetryCount}/{_config.MaxRetries} in {_config.RetryDelayMs}ms");
            }
            
            _api.Event.RegisterCallback(dt =>
            {
                CheckConnectionAndProcessQueue();
            }, _config.RetryDelayMs);
        }

        /// <summary>
        /// Checks connection status and processes queue if connected
        /// </summary>
        private void CheckConnectionAndProcessQueue()
        {
            _connectionRetryInProgress = false;
            
            if (IsConnected)
            {
                if (_config.EnableDebugLogging)
                {
                    _api.Logger.Debug($"{_config.LogPrefix} Channel is now connected, processing queued packet actions");
                }
                _connectionRetryCount = 0; // Reset retry count on successful connection
                ProcessPacketActionQueue();
            }
            else if (_pendingPacketActions.Count > 0)
            {
                if (_config.EnableDebugLogging)
                {
                    _api.Logger.Debug($"{_config.LogPrefix} Channel still not connected, retry {_connectionRetryCount}/{_config.MaxRetries}");
                }
                StartConnectionRetry(); // Try again
            }
        }

        /// <summary>
        /// Clears all pending packet actions to prevent memory buildup
        /// </summary>
        public void ClearPendingActions()
        {
            var clearedCount = _pendingPacketActions.Count;
            _pendingPacketActions.Clear();
            if (clearedCount > 0 && _config.EnableDebugLogging)
            {
                _api.Logger.Debug($"{_config.LogPrefix} Cleared {clearedCount} pending packet actions");
            }
        }

        /// <summary>
        /// Resets the retry mechanism state
        /// </summary>
        public void ResetRetryState()
        {
            _connectionRetryInProgress = false;
            _connectionRetryCount = 0;
            if (_config.EnableDebugLogging)
            {
                _api.Logger.Debug($"{_config.LogPrefix} Retry state reset");
            }
        }

        /// <summary>
        /// Disposes of the safe network channel, clearing any pending actions
        /// </summary>
        public void Dispose()
        {
            ClearPendingActions();
            ResetRetryState();
        }
    }
}