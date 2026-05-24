using System;
using System.Linq;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterAppearanceParticipant : IRpCharacterSwitchParticipant
{
    public string Code => "thebasics:appearance";

    public int Order => 100;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        var player = context.Player;
        var entity = player.Entity;
        var attributes = entity.WatchedAttributes;

        record.Appearance = new RpCharacterAppearanceSnapshot
        {
            CharacterClass = attributes.GetString("characterClass") ?? string.Empty,
            ExtraTraits = (attributes.GetStringArray("extraTraits") ?? Array.Empty<string>())
                .Where(trait => !string.IsNullOrWhiteSpace(trait))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SkinConfig = RpCharacterSnapshotUtilities.ToBytes(attributes.GetTreeAttribute("skinConfig")),
            VoiceType = attributes.GetString("voicetype") ?? string.Empty,
            VoicePitch = attributes.GetString("voicepitch") ?? string.Empty,
            SkinModel = attributes.GetString("skinModel") ?? string.Empty,
            DidSelectSkin = (player.WorldData as ServerWorldPlayerData)?.DidSelectSkin ?? false
        };
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        var snapshot = record.Appearance;
        if (snapshot == null || IsEmpty(snapshot))
        {
            return;
        }

        var player = context.Player;
        var entity = player.Entity;
        var attributes = entity.WatchedAttributes;
        var extraTraits = (snapshot.ExtraTraits ?? Enumerable.Empty<string>())
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        attributes.SetStringArray("extraTraits", extraTraits);
        attributes.MarkPathDirty("extraTraits");

        RestoreCharacterClass(entity, attributes, snapshot.CharacterClass);
        RestoreSkinConfig(attributes, snapshot.SkinConfig);
        RestoreVoiceAndModel(entity, attributes, snapshot);
        entity.MarkShapeModified();
        attributes.MarkPathDirty("skinConfig");

        if (player.WorldData is ServerWorldPlayerData worldData)
        {
            worldData.DidSelectSkin = snapshot.DidSelectSkin;
        }
    }

    private static void RestoreCharacterClass(EntityPlayer entity, TreeAttribute attributes, string characterClass)
    {
        if (string.IsNullOrWhiteSpace(characterClass))
        {
            return;
        }

        var characterSystem = entity.Api.ModLoader.GetModSystem<CharacterSystem>();
        if (characterSystem != null)
        {
            characterSystem.setCharacterClass(entity, characterClass, initializeGear: false);
            return;
        }

        attributes.SetString("characterClass", characterClass);
    }

    private static void RestoreSkinConfig(TreeAttribute attributes, byte[] skinConfigBytes)
    {
        var skinConfig = RpCharacterSnapshotUtilities.FromBytes(skinConfigBytes);
        if (skinConfig != null)
        {
            attributes.SetAttribute("skinConfig", skinConfig);
        }
    }

    private static void RestoreVoiceAndModel(EntityPlayer entity, TreeAttribute attributes, RpCharacterAppearanceSnapshot snapshot)
    {
        var voiceType = snapshot.VoiceType ?? string.Empty;
        var voicePitch = snapshot.VoicePitch ?? string.Empty;
        attributes.SetString("skinModel", snapshot.SkinModel ?? string.Empty);
        attributes.SetString("voicetype", voiceType);
        attributes.SetString("voicepitch", voicePitch);

        var skinnable = entity.GetBehavior<EntityBehaviorExtraSkinnable>();
        skinnable?.ApplyVoice(voiceType, voicePitch, testTalk: false);
    }

    private static bool IsEmpty(RpCharacterAppearanceSnapshot snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.CharacterClass) &&
               string.IsNullOrWhiteSpace(snapshot.VoiceType) &&
               string.IsNullOrWhiteSpace(snapshot.VoicePitch) &&
               string.IsNullOrWhiteSpace(snapshot.SkinModel) &&
               (snapshot.ExtraTraits == null || snapshot.ExtraTraits.Count == 0) &&
               (snapshot.SkinConfig == null || snapshot.SkinConfig.Length == 0) &&
               !snapshot.DidSelectSkin;
    }
}
