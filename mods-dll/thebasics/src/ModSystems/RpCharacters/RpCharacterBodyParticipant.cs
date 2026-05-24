using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterBodyParticipant : IRpCharacterSwitchParticipant
{
    public string Code => "thebasics:body";

    public int Order => 300;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        var player = context.Player;
        var entity = player.Entity;
        var worldData = player.WorldData as ServerWorldPlayerData;

        record.Body = new RpCharacterBodySnapshot
        {
            Health = CaptureHealth(entity.GetBehavior<EntityBehaviorHealth>()),
            Hunger = CaptureHunger(entity.GetBehavior<EntityBehaviorHunger>()),
            Intoxication = entity.WatchedAttributes.GetFloat("intoxication"),
            Psychedelic = entity.WatchedAttributes.GetFloat("psychedelic"),
            Position = CapturePosition(entity.Pos),
            PositionBeforeFalling = CapturePosition(entity.PositionBeforeFalling, entity.Pos.Dimension),
            Spawn = CaptureSpawn(worldData?.SpawnPosition),
            Deaths = worldData?.Deaths ?? player.WorldData?.Deaths ?? 0
        };
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        var snapshot = record.Body;
        if (snapshot == null || IsEmpty(snapshot))
        {
            return;
        }

        var player = context.Player;
        var entity = player.Entity;

        RestoreHealth(entity.GetBehavior<EntityBehaviorHealth>(), snapshot.Health);
        RestoreHunger(entity.GetBehavior<EntityBehaviorHunger>(), snapshot.Hunger);
        entity.WatchedAttributes.SetFloat("intoxication", snapshot.Intoxication);
        entity.WatchedAttributes.SetFloat("psychedelic", snapshot.Psychedelic);

        if (player.WorldData is ServerWorldPlayerData worldData)
        {
            worldData.Deaths = snapshot.Deaths;
        }

        RestoreSpawn(player, snapshot.Spawn);
        RestorePosition(entity, snapshot.Position, snapshot.PositionBeforeFalling);
        player.BroadcastPlayerData();
    }

    private static RpCharacterHealthSnapshot CaptureHealth(EntityBehaviorHealth health)
    {
        if (health == null)
        {
            return new RpCharacterHealthSnapshot();
        }

        return new RpCharacterHealthSnapshot
        {
            Available = true,
            Health = health.Health,
            PreviousHealth = health.PreviousHealth,
            HealthChangeRate = health.HealthChangeRate,
            BaseMaxHealth = health.BaseMaxHealth,
            MaxHealth = health.MaxHealth,
            HasFutureHealth = health.FutureHealth.HasValue,
            FutureHealth = health.FutureHealth ?? 0f
        };
    }

    private static void RestoreHealth(EntityBehaviorHealth health, RpCharacterHealthSnapshot snapshot)
    {
        if (health == null || snapshot?.Available != true)
        {
            return;
        }

        health.BaseMaxHealth = snapshot.BaseMaxHealth;
        health.MaxHealth = snapshot.MaxHealth;
        health.PreviousHealth = snapshot.PreviousHealth;
        health.HealthChangeRate = snapshot.HealthChangeRate;
        health.FutureHealth = snapshot.HasFutureHealth ? (float?)snapshot.FutureHealth : null;
        health.Health = snapshot.Health;
    }

    private static RpCharacterHungerSnapshot CaptureHunger(EntityBehaviorHunger hunger)
    {
        if (hunger == null)
        {
            return new RpCharacterHungerSnapshot();
        }

        return new RpCharacterHungerSnapshot
        {
            Available = true,
            Saturation = hunger.Saturation,
            MaxSaturation = hunger.MaxSaturation,
            FruitLevel = hunger.FruitLevel,
            VegetableLevel = hunger.VegetableLevel,
            ProteinLevel = hunger.ProteinLevel,
            GrainLevel = hunger.GrainLevel,
            DairyLevel = hunger.DairyLevel,
            SaturationLossDelayFruit = hunger.SaturationLossDelayFruit,
            SaturationLossDelayVegetable = hunger.SaturationLossDelayVegetable,
            SaturationLossDelayProtein = hunger.SaturationLossDelayProtein,
            SaturationLossDelayGrain = hunger.SaturationLossDelayGrain,
            SaturationLossDelayDairy = hunger.SaturationLossDelayDairy
        };
    }

    private static void RestoreHunger(EntityBehaviorHunger hunger, RpCharacterHungerSnapshot snapshot)
    {
        if (hunger == null || snapshot?.Available != true)
        {
            return;
        }

        hunger.MaxSaturation = snapshot.MaxSaturation;
        hunger.Saturation = snapshot.Saturation;
        hunger.FruitLevel = snapshot.FruitLevel;
        hunger.VegetableLevel = snapshot.VegetableLevel;
        hunger.ProteinLevel = snapshot.ProteinLevel;
        hunger.GrainLevel = snapshot.GrainLevel;
        hunger.DairyLevel = snapshot.DairyLevel;
        hunger.SaturationLossDelayFruit = snapshot.SaturationLossDelayFruit;
        hunger.SaturationLossDelayVegetable = snapshot.SaturationLossDelayVegetable;
        hunger.SaturationLossDelayProtein = snapshot.SaturationLossDelayProtein;
        hunger.SaturationLossDelayGrain = snapshot.SaturationLossDelayGrain;
        hunger.SaturationLossDelayDairy = snapshot.SaturationLossDelayDairy;
    }

    private static RpCharacterPositionSnapshot CapturePosition(EntityPos pos)
    {
        return new RpCharacterPositionSnapshot
        {
            Available = true,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Dimension = pos.Dimension,
            Yaw = pos.Yaw,
            Pitch = pos.Pitch,
            Roll = pos.Roll,
            HeadYaw = pos.HeadYaw,
            HeadPitch = pos.HeadPitch,
            MotionX = pos.Motion.X,
            MotionY = pos.Motion.Y,
            MotionZ = pos.Motion.Z
        };
    }

    private static RpCharacterPositionSnapshot CapturePosition(Vec3d pos, int dimension)
    {
        return new RpCharacterPositionSnapshot
        {
            Available = true,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Dimension = dimension
        };
    }

    private static void RestorePosition(EntityPlayer entity, RpCharacterPositionSnapshot position, RpCharacterPositionSnapshot positionBeforeFalling)
    {
        if (position?.Available != true)
        {
            return;
        }

        entity.Pos.Dimension = position.Dimension;
        entity.TeleportToDouble(position.X, position.Y, position.Z, () => ApplyPositionDetails(entity, position, positionBeforeFalling));
    }

    private static void ApplyPositionDetails(EntityPlayer entity, RpCharacterPositionSnapshot position, RpCharacterPositionSnapshot positionBeforeFalling)
    {
        entity.Pos.Dimension = position.Dimension;
        entity.Pos.Yaw = position.Yaw;
        entity.Pos.Pitch = position.Pitch;
        entity.Pos.Roll = position.Roll;
        entity.Pos.HeadYaw = position.HeadYaw;
        entity.Pos.HeadPitch = position.HeadPitch;
        entity.Pos.Motion.Set(position.MotionX, position.MotionY, position.MotionZ);

        if (positionBeforeFalling?.Available == true)
        {
            entity.PositionBeforeFalling.Set(positionBeforeFalling.X, positionBeforeFalling.Y, positionBeforeFalling.Z);
        }
        else
        {
            entity.PositionBeforeFalling.Set(position.X, position.Y, position.Z);
        }

        entity.WatchedAttributes.MarkAllDirty();
    }

    private static RpCharacterSpawnSnapshot CaptureSpawn(PlayerSpawnPos spawn)
    {
        if (spawn == null)
        {
            return new RpCharacterSpawnSnapshot();
        }

        return new RpCharacterSpawnSnapshot
        {
            HasSpawn = true,
            X = spawn.x,
            Y = spawn.y,
            Z = spawn.z,
            Yaw = spawn.yaw,
            Pitch = spawn.pitch,
            Roll = spawn.roll,
            RemainingUses = spawn.RemainingUses
        };
    }

    private static void RestoreSpawn(IServerPlayer player, RpCharacterSpawnSnapshot snapshot)
    {
        if (snapshot?.HasSpawn == true)
        {
            player.SetSpawnPosition(new PlayerSpawnPos(snapshot.X, snapshot.Y, snapshot.Z)
            {
                yaw = snapshot.Yaw,
                pitch = snapshot.Pitch,
                roll = snapshot.Roll,
                RemainingUses = snapshot.RemainingUses
            });
        }
        else
        {
            player.ClearSpawnPosition();
        }
    }

    private static bool IsEmpty(RpCharacterBodySnapshot snapshot)
    {
        return IsBodyStateUnavailable(snapshot) &&
               snapshot.Deaths == 0 &&
               IsNearlyZero(snapshot.Intoxication) &&
               IsNearlyZero(snapshot.Psychedelic);
    }

    private static bool IsBodyStateUnavailable(RpCharacterBodySnapshot snapshot)
    {
        return snapshot.Health?.Available != true &&
               snapshot.Hunger?.Available != true &&
               snapshot.Position?.Available != true &&
               snapshot.PositionBeforeFalling?.Available != true &&
               snapshot.Spawn?.HasSpawn != true;
    }

    private static bool IsNearlyZero(float value)
    {
        return value > -float.Epsilon && value < float.Epsilon;
    }
}
