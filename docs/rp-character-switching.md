# RP Character Switching

The BASICs RP character slots preserve the real Vintage Story account identity. `IPlayer.PlayerUID`, vanilla roles, groups, privileges, and `ServerPlayerData` remain account-scoped.

When `EnableRpCharacterSlots` and `EnableCharacterSheets` are enabled, `/character select` captures the active character and restores the target character through ordered switch participants.

Built-in participants capture:

- The BASICs identity projection: character sheet, nickname color, languages, default language, chat mode, chatter opt-in.
- Vanilla appearance: character class, extra traits, skin config, voice type, voice pitch, and optional `skinModel` watched attribute.
- Player inventories: `hotbar`, `backpack`, and `character` only.
- Body state: health, hunger/nutrition, intoxication, psychedelic effect, position, spawn position, and vanilla death count.

Switching is rejected when the player is dead, carrying a cursor item, has an external container open, or has input items in the crafting grid. After all validations pass, the switcher attempts to force-stop active hand use and unmount; if either operation fails, the switch is rejected.

The config surface is intentionally small: `EnableCharacterSheets` remains a prerequisite for RP character records, `EnableRpCharacterSlots` enables complete character switching, and `MaxRpCharacterSlots` controls the active slot limit. Partial identity-only, inventory-only, appearance-only, or body-only modes are not exposed as supported server configurations.

## Mod Integration

Other mods can register a participant from server-side startup:

```csharp
public override void StartServerSide(ICoreServerAPI api)
{
    api.ModLoader.GetModSystem<RpCharacterSystem>()
        ?.RegisterSwitchParticipant(new MyCharacterParticipant());
}
```

Participants implement `IRpCharacterSwitchParticipant`:

```csharp
public sealed class MyCharacterParticipant : IRpCharacterSwitchParticipant
{
    public string Code => "mymod:character-state";
    public int Order => 500;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        byte[] data = CaptureMyState(context.Player);
        record.SetExtensionSnapshot(Code, data);
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        byte[] data = record.GetExtensionSnapshot(Code);
        if (data != null)
        {
            RestoreMyState(context.Player, data);
        }
    }
}
```

Use a stable, namespaced `Code`. Registering another participant with the same code replaces the previous participant.

`Validate` should be side-effect free. If a participant must mutate player state before capture/restore, it can also implement `IRpCharacterSwitchPreparationParticipant`; `Prepare` runs only after all participants validate successfully.
