using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterRecord
{
    [ProtoMember(1)]
    public string CharacterId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string DisplayName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public bool Archived { get; set; }

    [ProtoMember(4)]
    public RpCharacterProjectionSnapshot Projection { get; set; } = new RpCharacterProjectionSnapshot();

    [ProtoMember(5)]
    public string CreatedUtc { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string ModifiedUtc { get; set; } = string.Empty;

    [ProtoMember(7)]
    public int SnapshotVersion { get; set; }

    [ProtoMember(8)]
    public RpCharacterAppearanceSnapshot Appearance { get; set; } = new RpCharacterAppearanceSnapshot();

    [ProtoMember(9)]
    public RpCharacterInventorySnapshot Inventory { get; set; } = new RpCharacterInventorySnapshot();

    [ProtoMember(10)]
    public RpCharacterBodySnapshot Body { get; set; } = new RpCharacterBodySnapshot();

    [ProtoMember(11)]
    public List<RpCharacterExtensionSnapshot> Extensions { get; set; } = new List<RpCharacterExtensionSnapshot>();

    public byte[] GetExtensionSnapshot(string key)
    {
        return Extensions?.FirstOrDefault(extension =>
            extension != null && string.Equals(extension.Key, key, StringComparison.OrdinalIgnoreCase))?.Data;
    }

    public void SetExtensionSnapshot(string key, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        Extensions ??= new List<RpCharacterExtensionSnapshot>();
        Extensions.RemoveAll(extension => extension == null || string.Equals(extension.Key, key, StringComparison.OrdinalIgnoreCase));
        if (data != null)
        {
            Extensions.Add(new RpCharacterExtensionSnapshot
            {
                Key = key,
                Data = data
            });
        }
    }
}
