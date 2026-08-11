using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.Tests.Support;

/// <summary>
/// Hand-written stand-in for <see cref="IServerPlayer"/>.
/// </summary>
/// <remarks>
/// These used to be NSubstitute substitutes. Vintage Story 1.22.6 added
/// <c>IPlayer.IsInInteractionRangeOf</c>, and Castle.Core cannot generate a working proxy for
/// <see cref="IPlayer"/> once it is present — every mocked-player test died with a
/// TypeLoadException before reaching an assertion. Confirmed against Castle 5.1.1 and 5.2.1, and
/// with raw <c>ProxyGenerator</c>, so it is not an NSubstitute problem and not fixable by upgrading.
///
/// Owning the double also removes the test suite's dependence on a proxy generator coping with a
/// large third-party interface that grows every game release.
///
/// Mod data is backed by a real dictionary, which is what makes the mod's own player extension
/// methods (nicknames, languages, chat modes, visual preferences) work here without extra setup —
/// they all read and write through <see cref="GetModdata"/>.
/// </remarks>
public sealed class FakeServerPlayer : IServerPlayer
{
    private readonly Dictionary<string, byte[]> _modData = new();
    private readonly Dictionary<string, object> _typedModData = new();

    public FakeServerPlayer(string playerUid = "player-1", string playerName = "Alice")
    {
        PlayerUID = playerUid;
        PlayerName = playerName;
    }

    /// <summary>Messages this player was sent, in order, for assertions.</summary>
    public List<(int GroupId, string Message, EnumChatType ChatType, string Data)> SentMessages { get; } = new();

    /// <summary>Arguments of each <see cref="BroadcastPlayerData"/> call, in order.</summary>
    public List<bool> BroadcastPlayerDataCalls { get; } = new();

    /// <summary>
    /// Decides <see cref="HasPrivilege"/>. Denies by default, matching what the NSubstitute
    /// substitutes this class replaced did, so privilege-denial tests keep their meaning.
    /// </summary>
    // System.Func must be qualified: Vintagestory.API.Common declares its own Func<>.
    public System.Func<string, bool> PrivilegeCheck { get; set; } = _ => false;

    public event OnEntityAction InWorldAction { add { } remove { } }

    // ---- IPlayer ----
    public string PlayerName { get; set; }
    public string PlayerUID { get; set; }
    public int ClientId { get; set; }
    public EntityPlayer Entity { get; set; }
    public IWorldPlayerData WorldData { get; set; }
    public IPlayerInventoryManager InventoryManager { get; set; }
    public string[] Privileges { get; set; } = [];
    public bool ImmersiveFpMode { get; set; }
    public IPlayerRole Role { get; set; }
    public PlayerGroupMembership[] Groups { get; set; } = [];
    public List<Entitlement> Entitlements { get; set; } = new();
    public BlockSelection CurrentBlockSelection { get; set; }
    public EntitySelection CurrentEntitySelection { get; set; }

    // ---- IServerPlayer ----
    public int ItemCollectMode { get; set; }
    public int CurrentChunkSentRadius { get; set; }
    public EnumClientState ConnectionState { get; set; }
    public string IpAddress { get; set; }
    public string LanguageCode { get; set; } = "en";
    public float Ping { get; set; }
    public IServerPlayerData ServerData { get; set; }

    public byte[] GetModdata(string key) => _modData.TryGetValue(key, out var value) ? value : null;

    // A null write is a removal, not a stored null. Several callers clear state that way.
    public void SetModdata(string key, byte[] data)
    {
        if (data == null)
        {
            _modData.Remove(key);
            return;
        }

        _modData[key] = data;
    }

    public void RemoveModdata(string key) => _modData.Remove(key);

    public T GetModData<T>(string key, T defaultValue = default) =>
        _typedModData.TryGetValue(key, out var value) ? (T)value : defaultValue;

    public void SetModData<T>(string key, T data) => _typedModData[key] = data;

    public PlayerGroupMembership[] GetGroups() => Groups;

    public PlayerGroupMembership GetGroup(int groupId) =>
        Groups.FirstOrDefault(group => group.GroupUid == groupId);

    public bool HasPrivilege(string privilegeCode) => PrivilegeCheck(privilegeCode);

    // These two are the members that broke Castle and forced this class into existence. Range
    // checks are irrelevant to the logic under test; tests that care about distance assert on
    // positions directly.
    public bool IsInInteractionRangeOf(Entity entity, float slack = 0.25f) => true;

    public bool IsInInteractionRangeOf(BlockPos pos, float slack = 0.25f) => true;

    public void SendMessage(int groupId, string message, EnumChatType chatType, string data = null) =>
        SentMessages.Add((groupId, message, chatType, data));

    public void BroadcastPlayerData(bool sendInventory = false) => BroadcastPlayerDataCalls.Add(sendInventory);

    public void SendIngameError(string code, string message = null, params object[] langparams) { }

    public void SendLocalisedMessage(int groupId, string message, params object[] args) { }

    public void Disconnect() { }

    public void Disconnect(string message) { }

    public void SetRole(string roleCode) { }

    public void SetSpawnPosition(PlayerSpawnPos pos) { }

    public void ClearSpawnPosition() { }

    public FuzzyEntityPos GetSpawnPosition(bool consumeSpawnUse) => throw new NotSupportedException();
}
