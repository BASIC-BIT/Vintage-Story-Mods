using System;
using System.Collections.Generic;
using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.Utilities
{
    /// <summary>
    /// Utility class for validating nickname conflicts and managing nickname-related operations.
    /// 
    /// CURRENT LIMITATION: Only checks nicknames of currently online players.
    /// 
    /// TODO: Implement offline player nickname checking
    /// 
    /// IMPLEMENTATION APPROACHES:
    /// 1. **ModData Iteration**: Loop through all PlayerDataManager.WorldDataByUID entries and check their moddata
    ///    - Use sapi.PlayerData (PlayerDataManager) to access WorldDataByUID dictionary
    ///    - For each ServerWorldPlayerData, call GetModdata("nickname") to check for nicknames
    ///    - Pros: Uses existing moddata system, no additional storage, works with current architecture
    ///    - Cons: Requires deserializing moddata for every offline player, potentially slow on large servers
    ///    - Note: ModData is stored in ServerWorldPlayerData (world-specific), not ServerPlayerData (server-wide)
    /// 
    /// 2. **Nickname Cache/Index**: Maintain a server-side cache of all nicknames
    ///    - Store nickname->playerUID mapping in world data or separate file
    ///    - Update cache when nicknames are set/cleared/player joins/leaves
    ///    - Pros: Fast lookups, no need to iterate all players
    ///    - Cons: Additional complexity, cache invalidation, memory usage, sync issues
    /// 
    /// 3. **Database/Persistent Storage**: Store nicknames in a dedicated data structure
    ///    - Use world.Api.ObjectCache or custom JSON/binary file
    ///    - Maintain bidirectional mapping: nickname->player and player->nickname
    ///    - Pros: Persistent, fast, can handle complex queries
    ///    - Cons: Most complex implementation, requires migration logic
    /// 
    /// RECOMMENDED APPROACH: Hybrid of #1 and #2 - ModData iteration with in-memory cache
    /// 
    /// HYBRID IMPLEMENTATION STRATEGY:
    /// 1. **Startup**: Use ModData iteration to build initial cache of all existing nicknames
    /// 2. **Runtime**: Maintain in-memory cache for fast lookups during nickname validation
    /// 3. **Cache Updates**: Update cache immediately when nicknames are set/cleared/players join
    /// 4. **No Cache Misses**: Since we're the authoritative source for nicknames, cache is always accurate
    /// 
    /// IMPLEMENTATION DETAILS:
    /// - Startup: Iterate through sapi.PlayerData.WorldDataByUID and build Dictionary<string, string> (nickname -> playerUID)
    /// - Cache: Store as static/instance variable in NicknameValidationUtils or RPProximityChatSystem
    /// - Updates: Update cache in SetNickname, ClearNickname, and HandleNicknameConflictsOnJoin methods
    /// - Validation: Fast O(1) lookup against cache instead of iterating all players
    /// 
    /// BENEFITS:
    /// - Fast validation (O(1) lookups after initial load)
    /// - Complete coverage (includes all offline players)
    /// - Simple implementation (no complex cache invalidation)
    /// - Authoritative source (we control all nickname changes)
    /// - Memory efficient (just a dictionary of strings)
    /// 
    /// MIGRATION CONSIDERATIONS:
    /// - Need to handle existing offline players who already have nicknames
    /// - Consider performance impact of checking thousands of offline players
    /// - May need to implement lazy loading or background processing for large servers
    /// - World-specific data means nicknames are per-world, not server-wide
    /// 
    /// EXISTING CONFLICT RESOLUTION:
    /// During initial cache population, we may discover players who already have conflicting nicknames
    /// that were set before this conflict prevention system was implemented. Resolution strategy:
    /// 
    /// **HYBRID APPROACH:**
    /// 1. **Cache Population**: When conflicts found, pick one "winner" to store in cache (e.g., most recent)
    /// 2. **Conflict Logging**: Log all conflicts for admin awareness, but don't force immediate resolution
    /// 3. **Natural Resolution**: When conflicted players join, HandleNicknameConflictsOnJoin() will automatically
    ///    reset conflicting nicknames, leaving only the cached "winner"
    /// 4. **No Forced Changes**: Don't proactively clear anyone's nickname during startup
    /// 
    /// **BENEFITS:**
    /// - Cache maintains O(1) lookup performance with Dictionary<string, string> structure
    /// - No immediate disruption to offline players
    /// - Conflicts resolve naturally as players come online
    /// - Admin visibility into existing conflicts through logs
    /// - System works immediately for new nickname validation
    /// </summary>
    public static class NicknameValidationUtils
    {
        /// <summary>
        /// Validates if a nickname conflicts with existing player names or nicknames
        /// </summary>
        /// <param name="player">The player trying to set the nickname</param>
        /// <param name="nickname">The nickname to validate</param>
        /// <param name="sapi">The server API instance</param>
        /// <param name="conflictingPlayer">The name of the conflicting player if any</param>
        /// <param name="conflictType">The type of conflict (username or nickname)</param>
        /// <returns>True if nickname is valid, false if it conflicts</returns>
        public static bool ValidateNickname(IServerPlayer player, string nickname, ICoreServerAPI sapi, out string conflictingPlayer, out string conflictType)
        {
            conflictingPlayer = null;
            conflictType = null;

            if (string.IsNullOrWhiteSpace(nickname))
            {
                return true; // Empty nickname is valid (will use player name)
            }

            // Check against online players' nicknames
            foreach (IPlayer onlinePlayer in sapi.World.AllOnlinePlayers)
            {
                if (onlinePlayer.PlayerUID == player.PlayerUID) continue; // Skip self

                var serverPlayer = onlinePlayer as IServerPlayer;
                if (serverPlayer != null)
                {
                    var existingNickname = serverPlayer.GetNickname();
                    if (existingNickname.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        conflictingPlayer = serverPlayer.PlayerName;
                        conflictType = "nickname";
                        return false;
                    }
                }
            }

            // Check against all player usernames (both online and offline)
            foreach (var playerDataPair in sapi.PlayerData.PlayerDataByUid)
            {
                if (playerDataPair.Key == player.PlayerUID) continue; // Skip self

                var playerData = playerDataPair.Value;
                if (playerData == null || playerData.LastKnownPlayername == null) continue; // Skip missing

                if (playerData.LastKnownPlayername.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                {
                    conflictingPlayer = playerData.LastKnownPlayername;
                    conflictType = "username";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles nickname conflicts when a player joins - resets any existing nicknames that match the joining player's username
        /// </summary>
        /// <param name="joiningPlayer">The player who just joined</param>
        /// <param name="sapi">The server API instance</param>
        /// <returns>List of players whose nicknames were reset</returns>
        public static List<string> HandleNicknameConflictsOnJoin(IServerPlayer joiningPlayer, ICoreServerAPI sapi)
        {
            var resetPlayers = new List<string>();
            
            // Check all online players for nickname conflicts with the joining player's username
            foreach (IPlayer onlinePlayer in sapi.World.AllOnlinePlayers)
            {
                if (onlinePlayer.PlayerUID == joiningPlayer.PlayerUID) continue; // Skip the joining player themselves

                var serverPlayer = onlinePlayer as IServerPlayer;
                if (serverPlayer != null && serverPlayer.HasNickname())
                {
                    var existingNickname = serverPlayer.GetNickname();
                    if (existingNickname.Equals(joiningPlayer.PlayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Reset conflicting nickname
                        serverPlayer.ClearNickname();
                        resetPlayers.Add(serverPlayer.PlayerName);
                        
                        // Notify the affected player
                        serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, 
                            $"Your nickname '{existingNickname}' has been reset because player '{joiningPlayer.PlayerName}' joined the server.", 
                            EnumChatType.Notification);
                    }
                }
            }
            
            return resetPlayers;
        }
    }
} 