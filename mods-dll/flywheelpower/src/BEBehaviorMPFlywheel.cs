using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BEBehaviorMPFlywheel : BEBehaviorMPBase
{
    private const float SecondsPerMechanicalTick = 0.02f;
    private const float VanillaAngleScale = 5f;
    private const float PassiveResistance = 0.0005f;
    private const float Epsilon = 0.00001f;
    private const int MinimumSparkIntervalTicks = 8;
    private const int SparkIntervalJitterTicks = 8;
    private const float SpeedTrendSmoothing = 0.25f;
    private const float SpeedTrendWindowSeconds = 0.5f;
    private const float NetworkSpeedDampingSeconds = 0.5f;

    private CompositeShape axleShape;
    private CompositeShape flywheelShape;
    private CompositeShape horizontalStandShape;
    private CompositeShape verticalStandShape;
    private CompositeShape standShape;
    private FlywheelDiskRenderable flywheelRenderable;
    private FlywheelStandRenderable standRenderable;
    private float flywheelSpeed;
    private float inertia = 8f;
    private float couplingStrength = 0.8f;
    private float maxTransferTorque = 0.35f;
    private float baseBearingLoss = 0.001f;
    private float viscousBearingLoss = 0.003f;
    private float windageLoss = 0.0015f;
    private float safeSpeed = 3.5f;
    private float couplingRampSeconds = 1.5f;
    private float rotatingMassKg;
    private float couplingEngagement;
    private bool slipCoupled = true;
    private long lastTorqueTick = -1;
    private float lastNetworkSpeed;
    private float lastTransferTorque;
    private float lastReturnedTorque;
    private float smoothedSpeedChangePerSecond;
    private float lastResistance;
    private float lastLossTorque;
    private float filteredNetworkSpeed;
    private bool hasFilteredNetworkSpeed;
    private float trendWindowStartSpeed;
    private float trendWindowElapsedSeconds;
    private bool trendWindowInitialized;
    private float flywheelAngleRad;
    private long lastAngleMs;
    private long nextSlipSparkTick;
    private long nextOverspeedSparkTick;
    private long nextOverspeedSmokeTick;

    public override float AngleRad => base.AngleRad;

    public BEBehaviorMPFlywheel(BlockEntity blockentity)
        : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        axleShape = properties["axleShape"].AsObject<CompositeShape>();
        flywheelShape = properties["flywheelShape"].AsObject<CompositeShape>();
        horizontalStandShape = properties["horizontalStandShape"].AsObject<CompositeShape>();
        verticalStandShape = properties["verticalStandShape"].AsObject<CompositeShape>();
        inertia = properties["inertia"].AsFloat(inertia);
        couplingStrength = properties["couplingStrength"].AsFloat(couplingStrength);
        maxTransferTorque = properties["maxTransferTorque"].AsFloat(maxTransferTorque);
        baseBearingLoss = properties["baseBearingLoss"].AsFloat(baseBearingLoss);
        viscousBearingLoss = properties["viscousBearingLoss"].AsFloat(viscousBearingLoss);
        windageLoss = properties["windageLoss"].AsFloat(windageLoss);
        safeSpeed = properties["safeSpeed"].AsFloat(safeSpeed);
        couplingRampSeconds = properties["couplingRampSeconds"].AsFloat(couplingRampSeconds);
        slipCoupled = properties["slipCoupled"].AsBool(slipCoupled);
        FlywheelPhysicalProfile physicalProfile = FlywheelPhysicalProperties.ForBlock(Blockentity.Block, properties["inertia"].AsFloat(inertia));
        inertia = physicalProfile.EffectiveInertia;
        rotatingMassKg = physicalProfile.RotatingMassKg;

        SetAxisAndShapeFromRotation();
        base.Initialize(api, properties);

        if (api.Side == EnumAppSide.Client && flywheelShape != null)
        {
            if (standShape != null)
            {
                standRenderable = new FlywheelStandRenderable(this);
                manager.AddDeviceForRender(standRenderable);
            }

            flywheelRenderable = new FlywheelDiskRenderable(this);
            manager.AddDeviceForRender(flywheelRenderable);
        }
    }

    public override void SetOrientations()
    {
        SetAxisAndShapeFromRotation();
    }

    public override float GetResistance()
    {
        return PassiveResistance;
    }

    public override void SetPropagationDirection(MechPowerPath path)
    {
        BlockFacing nextDirection = path?.NetworkDir();
        bool directionRebased = FlywheelDirectionRebase.IsOpposite(propagationDir, nextDirection);

        base.SetPropagationDirection(path);

        if (directionRebased)
        {
            (flywheelSpeed, lastNetworkSpeed, lastTransferTorque, flywheelAngleRad) =
                FlywheelDirectionRebase.Rebase(flywheelSpeed, lastNetworkSpeed, lastTransferTorque, flywheelAngleRad);
            filteredNetworkSpeed = -filteredNetworkSpeed;
            lastReturnedTorque = 0f;
        }
    }

    public override float GetTorque(long tick, float speed, out float resistance)
    {
        float dt = GetDeltaTime(tick);
        float intervalNetworkSpeed = FlywheelCouplingMath.DampNetworkSpeed(
            filteredNetworkSpeed,
            speed,
            hasFilteredNetworkSpeed,
            dt,
            NetworkSpeedDampingSeconds);
        filteredNetworkSpeed = intervalNetworkSpeed;
        hasFilteredNetworkSpeed = true;
        if (!slipCoupled)
        {
            flywheelSpeed = speed;
            lastNetworkSpeed = speed;
            lastTransferTorque = 0f;
            lastReturnedTorque = 0f;
            smoothedSpeedChangePerSecond = 0f;
            ResetSpeedTrend();
            lastLossTorque = FlywheelCouplingMath.GetLossTorque(
                Math.Abs(speed),
                baseBearingLoss,
                viscousBearingLoss,
                windageLoss,
                safeSpeed);
            resistance = PassiveResistance + lastLossTorque;
            lastResistance = resistance;

            if (Api.Side == EnumAppSide.Server && tick % 10 == 0)
            {
                Blockentity.MarkDirty(false);
            }

            return 0f;
        }

        couplingEngagement = Math.Min(1f, couplingEngagement + dt / Math.Max(couplingRampSeconds, Epsilon));
        FlywheelStep step = FlywheelCouplingMath.Step(
            flywheelSpeed,
            intervalNetworkSpeed,
            new FlywheelStepParameters(
                inertia,
                couplingStrength,
                couplingEngagement,
                maxTransferTorque,
                baseBearingLoss,
                viscousBearingLoss,
                windageLoss,
                safeSpeed),
            dt);
        flywheelSpeed = step.Speed;
        float transferTorque = step.TransferTorque;
        lastLossTorque = step.LossTorque;
        UpdateSpeedTrend(dt);

        bool drivesNetwork = Math.Abs(intervalNetworkSpeed) < Epsilon
            ? Math.Abs(transferTorque) > Epsilon
            : transferTorque * intervalNetworkSpeed > 0f;

        resistance = PassiveResistance;
        float torque = 0f;
        if (drivesNetwork)
        {
            torque = transferTorque;
        }
        else
        {
            resistance += Math.Abs(transferTorque);
        }

        lastNetworkSpeed = intervalNetworkSpeed;
        lastTransferTorque = transferTorque;
        lastReturnedTorque = torque;
        lastResistance = resistance;

        if (Api.Side == EnumAppSide.Server)
        {
            SpawnSlipSparks(tick);
            SpawnOverspeedSparks(tick);
            SpawnOverspeedSmoke(tick);
            if (tick % 10 == 0)
            {
                Blockentity.MarkDirty(false);
            }
        }

        return torque;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        BlockFacing previousDirection = propagationDir;
        flywheelSpeed = tree.GetFloat("flywheelSpeed");
        lastNetworkSpeed = tree.GetFloat("lastNetworkSpeed");
        lastTransferTorque = tree.GetFloat("lastTransferTorque");
        lastReturnedTorque = tree.GetFloat("lastReturnedTorque");
        smoothedSpeedChangePerSecond = tree.GetFloat("smoothedSpeedChangePerSecond");
        lastResistance = tree.GetFloat("lastResistance");
        lastLossTorque = tree.GetFloat("lastLossTorque");
        base.FromTreeAttributes(tree, worldAccessForResolve);

        // Speeds arrive already signed in the server's current direction basis.
        // Mirror only the local render phase when the serialized basis reverses.
        if (FlywheelDirectionRebase.IsOpposite(previousDirection, propagationDir))
        {
            flywheelAngleRad = FlywheelDirectionRebase.MirrorAngle(flywheelAngleRad);
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("flywheelSpeed", flywheelSpeed);
        tree.SetFloat("lastNetworkSpeed", lastNetworkSpeed);
        tree.SetFloat("lastTransferTorque", lastTransferTorque);
        tree.SetFloat("lastReturnedTorque", lastReturnedTorque);
        tree.SetFloat("smoothedSpeedChangePerSecond", smoothedSpeedChangePerSecond);
        tree.SetFloat("lastResistance", lastResistance);
        tree.SetFloat("lastLossTorque", lastLossTorque);
        base.ToTreeAttributes(tree);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
        if (!FlywheelPowerModSystem.Config.ShowDebugBlockInfo)
        {
            return;
        }

        float displayFlywheelSpeed = GetDisplayFlywheelSpeed();
        float speedAbs = Math.Abs(displayFlywheelSpeed);
        float safeRatio = FlywheelSafety.GetRatedSpeedRatio(speedAbs, safeSpeed);
        double storedPercent = FlywheelSafety.GetStoredEnergyPercent(speedAbs, safeSpeed);
        float couplingLoadPercent = FlywheelTelemetry.GetCouplingLoadPercent(lastTransferTorque, maxTransferTorque);
        float slipPercent = FlywheelTelemetry.GetSlipPercent(displayFlywheelSpeed, lastNetworkSpeed, safeSpeed);

        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-state", GetStateLabel()));
        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-speed", Math.Round(displayFlywheelSpeed, 2), Math.Round(lastNetworkSpeed, 2)));
        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-energy", Math.Round(storedPercent, 0)));
        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-physical", Math.Round(rotatingMassKg), Math.Round(inertia, 3)));
        string torqueKey = slipCoupled ? "flywheelpower:blockinfo-coupling" : "flywheelpower:blockinfo-shaft";
        string couplingLimit = slipCoupled && couplingLoadPercent >= 99.5f
            ? Lang.Get("flywheelpower:blockinfo-coupling-limit")
            : string.Empty;
        sb.AppendLine(Lang.Get(torqueKey, Math.Round(couplingLoadPercent), couplingLimit));
        if (slipCoupled)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-mismatch", Math.Round(slipPercent)));
        }

        if (IsActivelySlipping())
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-slipping"));
        }

        if (lastLossTorque > 0.02f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-losses", Math.Round(lastLossTorque, 3)));
        }

        double ratedSpeedPercent = Math.Round(safeRatio * 100f);
        if (safeRatio > 1f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-overspeed", ratedSpeedPercent));
        }
        else if (safeRatio >= 0.9f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-near-limit", ratedSpeedPercent));
        }
    }

    protected override CompositeShape GetShape()
    {
        return axleShape ?? base.GetShape();
    }

    public override void OnBlockRemoved()
    {
        RemoveExtraRenderables();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        RemoveExtraRenderables();
        base.OnBlockUnloaded();
    }

    private float GetDeltaTime(long tick)
    {
        if (lastTorqueTick < 0)
        {
            lastTorqueTick = tick;
            couplingEngagement = 0f;
            return SecondsPerMechanicalTick;
        }

        long tickDelta = Math.Max(1, tick - lastTorqueTick);
        lastTorqueTick = tick;
        if (tickDelta > 5)
        {
            couplingEngagement = 0f;
            hasFilteredNetworkSpeed = false;
            ResetSpeedTrend();
        }

        return Math.Min(0.25f, tickDelta * SecondsPerMechanicalTick);
    }

    private string GetStateLabel()
    {
        if (!slipCoupled)
        {
            return Math.Abs(GetDisplayFlywheelSpeed()) > 0.01f
                ? Lang.Get("flywheelpower:blockinfo-state-coasting")
                : Lang.Get("flywheelpower:blockinfo-state-idle");
        }

        float gearedRatio = GearedRatio;
        float networkSpeed = Network?.Speed ?? lastNetworkSpeed;
        float ownNetworkTorque = gearedRatio * lastReturnedTorque;
        float ownNetworkResistance = Math.Abs(gearedRatio) * lastResistance
            + networkSpeed * networkSpeed * gearedRatio * gearedRatio / 1000f;
        FlywheelOperatingState state = FlywheelTelemetry.GetOperatingState(
            flywheelSpeed,
            smoothedSpeedChangePerSecond,
            Network?.NetworkTorque ?? ownNetworkTorque,
            ownNetworkTorque,
            Network?.NetworkResistance ?? ownNetworkResistance,
            ownNetworkResistance,
            maxTransferTorque);
        double speedChange = Math.Round(Math.Abs(smoothedSpeedChangePerSecond), 2);

        return state switch
        {
            FlywheelOperatingState.Coasting when smoothedSpeedChangePerSecond < -0.005f =>
                Lang.Get("flywheelpower:blockinfo-state-coasting-slowing", speedChange),
            FlywheelOperatingState.Coasting => Lang.Get("flywheelpower:blockinfo-state-coasting"),
            FlywheelOperatingState.CoastingUnderLoad =>
                Lang.Get("flywheelpower:blockinfo-state-coasting-load", speedChange),
            FlywheelOperatingState.Charging =>
                Lang.Get("flywheelpower:blockinfo-state-charging", speedChange),
            FlywheelOperatingState.Discharging =>
                Lang.Get("flywheelpower:blockinfo-state-discharging", speedChange),
            FlywheelOperatingState.DrivenHoldingSpeed =>
                Lang.Get("flywheelpower:blockinfo-state-driven-steady"),
            _ => Lang.Get("flywheelpower:blockinfo-state-idle")
        };
    }

    private float GetDisplayFlywheelSpeed()
    {
        return slipCoupled ? flywheelSpeed : lastNetworkSpeed;
    }

    private void SetAxisAndShapeFromRotation()
    {
        string rotation = Blockentity.Block?.Variant["rotation"];
        switch (rotation)
        {
            case "we":
                standShape = horizontalStandShape;
                AxisSign = new[] { -1, 0, 0 };
                OutFacingForNetworkDiscovery = BlockFacing.EAST;
                SetShapeRotation(0f, 0f, 0f, 0f, 0f, 0f);
                break;
            case "ud":
                standShape = verticalStandShape;
                AxisSign = new[] { 0, 1, 0 };
                OutFacingForNetworkDiscovery = BlockFacing.UP;
                SetShapeRotation(0f, 0f, 90f, 0f, 0f, 0f);
                break;
            default:
                standShape = horizontalStandShape;
                AxisSign = new[] { 0, 0, -1 };
                OutFacingForNetworkDiscovery = BlockFacing.SOUTH;
                SetShapeRotation(0f, 90f, 0f, 0f, 90f, 0f);
                break;
        }
    }

    private void SetShapeRotation(
        float rotatingX,
        float rotatingY,
        float rotatingZ,
        float standX,
        float standY,
        float standZ)
    {
        SetShapeRotation(axleShape, rotatingX, rotatingY, rotatingZ);
        SetShapeRotation(flywheelShape, rotatingX, rotatingY, rotatingZ);
        SetShapeRotation(standShape, standX, standY, standZ);
    }

    private static void SetShapeRotation(CompositeShape shape, float rotateX, float rotateY, float rotateZ)
    {
        if (shape == null)
        {
            return;
        }

        shape.rotateX = rotateX;
        shape.rotateY = rotateY;
        shape.rotateZ = rotateZ;

        if (shape.Overlays == null)
        {
            return;
        }

        foreach (CompositeShape overlay in shape.Overlays)
        {
            overlay.rotateX = rotateX;
            overlay.rotateY = rotateY;
            overlay.rotateZ = rotateZ;
        }
    }

    private float GetFlywheelAngleRad()
    {
        if (!slipCoupled)
        {
            return AngleRad;
        }

        if (Api?.Side == EnumAppSide.Client)
        {
            long nowMs = Api.World.ElapsedMilliseconds;
            if (lastAngleMs > 0)
            {
                float dt = Math.Min(0.25f, (nowMs - lastAngleMs) / 1000f);
                flywheelAngleRad = GameMath.Mod(flywheelAngleRad + flywheelSpeed * dt * VanillaAngleScale, GameMath.TWOPI);
            }
            lastAngleMs = nowMs;
        }

        return IsRotationReversed()
            ? GameMath.Mod(GameMath.TWOPI - flywheelAngleRad, GameMath.TWOPI)
            : flywheelAngleRad;
    }

    private void RemoveExtraRenderables()
    {
        if (flywheelRenderable != null)
        {
            manager?.RemoveDeviceForRender(flywheelRenderable);
            flywheelRenderable = null;
        }

        if (standRenderable != null)
        {
            manager?.RemoveDeviceForRender(standRenderable);
            standRenderable = null;
        }
    }

    private bool IsActivelySlipping()
    {
        return slipCoupled && FlywheelTelemetry.IsActivelySlipping(
            flywheelSpeed,
            lastNetworkSpeed,
            safeSpeed);
    }

    private void SpawnSlipSparks(long tick)
    {
        if (!IsActivelySlipping() || tick < nextSlipSparkTick)
        {
            return;
        }

        Random random = Api.World.Rand;
        nextSlipSparkTick = tick + MinimumSparkIntervalTicks + random.Next(SparkIntervalJitterTicks + 1);
        Vec3d origin = GetHubParticleOrigin(random, out double radialX, out double radialY, out double radialZ, out double side);
        var particles = new SimpleParticleProperties
        {
            LifeLength = 0.5f,
            Color = ColorUtil.ToRgba(255, 255, 185, 45),
            GravityEffect = 0.35f,
            ParticleModel = EnumParticleModel.Cube,
            MinPos = origin,
            AddPos = new Vec3d(0.04, 0.04, 0.04),
            SelfPropelled = true,
            MinVelocity = new Vec3f(
                (float)(radialX * 0.9 + AxisSign[0] * side * 0.3),
                (float)(radialY * 0.9 + AxisSign[1] * side * 0.3 + 0.25),
                (float)(radialZ * 0.9 + AxisSign[2] * side * 0.3)),
            AddVelocity = new Vec3f(0.45f, 0.35f, 0.45f),
            ShouldDieInAir = false,
            ShouldSwimOnLiquid = false,
            ShouldDieInLiquid = false,
            WithTerrainCollision = false,
            MinSize = 0.1f,
            MaxSize = 0.16f,
            WindAffected = false,
            MinQuantity = 8f,
            DieOnRainHeightmap = false
        };
        Api.World.SpawnParticles(particles);
    }

    private void SpawnOverspeedSparks(long tick)
    {
        int interval = FlywheelSafety.GetOverspeedSparkIntervalTicks(flywheelSpeed, safeSpeed);
        if (interval == int.MaxValue || tick < nextOverspeedSparkTick)
        {
            return;
        }

        Random random = Api.World.Rand;
        nextOverspeedSparkTick = tick + interval;
        Vec3d origin = GetHubParticleOrigin(random, out double radialX, out double radialY, out double radialZ, out double side);
        var particles = new SimpleParticleProperties
        {
            LifeLength = 0.65f,
            Color = ColorUtil.ToRgba(255, 255, 70, 15),
            GravityEffect = 0.45f,
            ParticleModel = EnumParticleModel.Cube,
            MinPos = origin,
            AddPos = new Vec3d(0.06, 0.06, 0.06),
            SelfPropelled = true,
            MinVelocity = new Vec3f(
                (float)(radialX * 1.3 + AxisSign[0] * side * 0.45),
                (float)(radialY * 1.3 + AxisSign[1] * side * 0.45 + 0.35),
                (float)(radialZ * 1.3 + AxisSign[2] * side * 0.45)),
            AddVelocity = new Vec3f(0.6f, 0.5f, 0.6f),
            ShouldDieInAir = false,
            ShouldSwimOnLiquid = false,
            ShouldDieInLiquid = false,
            WithTerrainCollision = false,
            MinSize = 0.12f,
            MaxSize = 0.2f,
            WindAffected = false,
            MinQuantity = FlywheelSafety.GetOverspeedSparkQuantity(flywheelSpeed, safeSpeed),
            DieOnRainHeightmap = false
        };
        Api.World.SpawnParticles(particles);
    }

    private void SpawnOverspeedSmoke(long tick)
    {
        int interval = FlywheelSafety.GetOverspeedSmokeIntervalTicks(flywheelSpeed, safeSpeed);
        if (interval == int.MaxValue || tick < nextOverspeedSmokeTick)
        {
            return;
        }

        Random random = Api.World.Rand;
        nextOverspeedSmokeTick = tick + interval;
        Vec3d origin = GetHubParticleOrigin(random, out _, out _, out _, out _);
        var particles = new SimpleParticleProperties
        {
            LifeLength = 1.8f,
            addLifeLength = 0.5f,
            Color = ColorUtil.ToRgba(110, 75, 70, 65),
            GravityEffect = -0.00625f,
            ParticleModel = EnumParticleModel.Quad,
            MinPos = origin,
            AddPos = new Vec3d(0.08, 0.04, 0.08),
            MinVelocity = new Vec3f(-0.04f, 0.12f, -0.04f),
            AddVelocity = new Vec3f(0.08f, 0.12f, 0.08f),
            ShouldDieInAir = false,
            ShouldSwimOnLiquid = false,
            ShouldDieInLiquid = true,
            WithTerrainCollision = false,
            MinSize = 0.18f,
            MaxSize = 0.35f,
            WindAffected = true,
            MinQuantity = FlywheelSafety.GetOverspeedSmokeQuantity(flywheelSpeed, safeSpeed),
            AddQuantity = 0.5f,
            DieOnRainHeightmap = false
        };
        Api.World.SpawnParticles(particles);
    }

    private Vec3d GetHubParticleOrigin(
        Random random,
        out double radialX,
        out double radialY,
        out double radialZ,
        out double side)
    {
        double angle = random.NextDouble() * GameMath.TWOPI;
        if (Math.Abs(AxisSign[0]) > 0)
        {
            radialX = 0;
            radialY = Math.Cos(angle);
            radialZ = Math.Sin(angle);
        }
        else if (Math.Abs(AxisSign[1]) > 0)
        {
            radialX = Math.Cos(angle);
            radialY = 0;
            radialZ = Math.Sin(angle);
        }
        else
        {
            radialX = Math.Cos(angle);
            radialY = Math.Sin(angle);
            radialZ = 0;
        }

        bool compact = Blockentity.Block?.Code?.Path?.StartsWith("compactflywheel", StringComparison.Ordinal) == true;
        double radius = (compact ? FlywheelModelDimensions.CompactHubOuterRadius : FlywheelModelDimensions.HubOuterRadius) + 0.03;
        double axialDepth = (compact ? FlywheelModelDimensions.CompactHubHalfThickness : FlywheelModelDimensions.HubHalfThickness) + 0.03;
        side = random.Next(2) == 0 ? -1 : 1;
        return new Vec3d(
            Position.X + 0.5 + radialX * radius + AxisSign[0] * side * axialDepth,
            Position.Y + 0.5 + radialY * radius + AxisSign[1] * side * axialDepth,
            Position.Z + 0.5 + radialZ * radius + AxisSign[2] * side * axialDepth);
    }

    private void UpdateSpeedTrend(float dt)
    {
        float speedAbs = Math.Abs(flywheelSpeed);
        if (!trendWindowInitialized)
        {
            trendWindowStartSpeed = speedAbs;
            trendWindowElapsedSeconds = 0f;
            trendWindowInitialized = true;
            smoothedSpeedChangePerSecond = 0f;
            return;
        }

        trendWindowElapsedSeconds += dt;
        if (trendWindowElapsedSeconds < SpeedTrendWindowSeconds)
        {
            return;
        }

        float measuredSpeedChange = (speedAbs - trendWindowStartSpeed)
            / Math.Max(trendWindowElapsedSeconds, Epsilon);
        smoothedSpeedChangePerSecond +=
            (measuredSpeedChange - smoothedSpeedChangePerSecond) * SpeedTrendSmoothing;
        trendWindowStartSpeed = speedAbs;
        trendWindowElapsedSeconds = 0f;
    }

    private void ResetSpeedTrend()
    {
        trendWindowStartSpeed = Math.Abs(flywheelSpeed);
        trendWindowElapsedSeconds = 0f;
        trendWindowInitialized = false;
    }

    private sealed class FlywheelStandRenderable : IMechanicalPowerRenderable
    {
        private readonly BEBehaviorMPFlywheel owner;

        public FlywheelStandRenderable(BEBehaviorMPFlywheel owner)
        {
            this.owner = owner;
        }

        public float AngleRad => 0f;

        public Block Block => owner.Blockentity.Block;

        public BlockPos Position => owner.Position;

        public Vec4f LightRgba => owner.LightRgba;

        public int[] AxisSign => owner.AxisSign;

        public CompositeShape Shape => owner.standShape;
    }

    private sealed class FlywheelDiskRenderable : IMechanicalPowerRenderable
    {
        private readonly BEBehaviorMPFlywheel owner;

        public FlywheelDiskRenderable(BEBehaviorMPFlywheel owner)
        {
            this.owner = owner;
        }

        public float AngleRad => owner.GetFlywheelAngleRad();

        public Block Block => owner.Blockentity.Block;

        public BlockPos Position => owner.Position;

        public Vec4f LightRgba => owner.LightRgba;

        public int[] AxisSign => owner.AxisSign;

        public CompositeShape Shape => owner.flywheelShape;
    }
}
