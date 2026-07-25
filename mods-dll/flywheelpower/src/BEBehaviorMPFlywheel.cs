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
    private const float StateSpeedDeltaTolerance = 0.00025f;
    private const float IronDensity = 7800f;

    private CompositeShape axleShape;
    private CompositeShape flywheelShape;
    private FlywheelDiskRenderable flywheelRenderable;
    private float flywheelSpeed;
    private float inertia = 8f;
    private float couplingStrength = 0.8f;
    private float maxTransferTorque = 0.35f;
    private float baseBearingLoss = 0.001f;
    private float viscousBearingLoss = 0.003f;
    private float windageLoss = 0.0015f;
    private float safeSpeed = 3.5f;
    private float couplingRampSeconds = 1.5f;
    private float wheelOuterRadius = FlywheelModelDimensions.WheelOuterRadius;
    private float coupledInnerRadius = FlywheelModelDimensions.CoupledInnerRadius;
    private float wheelHalfThickness = FlywheelModelDimensions.WheelHalfThickness;
    private float couplingEngagement;
    private bool slipCoupled = true;
    private long lastTorqueTick = -1;
    private float lastNetworkSpeed;
    private float lastTransferTorque;
    private float lastTransferEnergyDelta;
    private float lastResistance;
    private float lastLossTorque;
    private float flywheelAngleRad;
    private long lastAngleMs;

    public override float AngleRad => base.AngleRad;

    public BEBehaviorMPFlywheel(BlockEntity blockentity)
        : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        axleShape = properties["axleShape"].AsObject<CompositeShape>();
        flywheelShape = properties["flywheelShape"].AsObject<CompositeShape>();
        inertia = properties["inertia"].AsFloat(inertia);
        couplingStrength = properties["couplingStrength"].AsFloat(couplingStrength);
        maxTransferTorque = properties["maxTransferTorque"].AsFloat(maxTransferTorque);
        baseBearingLoss = properties["baseBearingLoss"].AsFloat(baseBearingLoss);
        viscousBearingLoss = properties["viscousBearingLoss"].AsFloat(viscousBearingLoss);
        windageLoss = properties["windageLoss"].AsFloat(windageLoss);
        safeSpeed = properties["safeSpeed"].AsFloat(safeSpeed);
        couplingRampSeconds = properties["couplingRampSeconds"].AsFloat(couplingRampSeconds);
        wheelOuterRadius = properties["wheelOuterRadius"].AsFloat(wheelOuterRadius);
        coupledInnerRadius = properties["coupledInnerRadius"].AsFloat(coupledInnerRadius);
        wheelHalfThickness = properties["wheelHalfThickness"].AsFloat(wheelHalfThickness);
        slipCoupled = properties["slipCoupled"].AsBool(slipCoupled);
        inertia = ComputeVariantInertia(properties["inertia"].AsFloat(inertia));

        SetAxisAndShapeFromRotation();
        base.Initialize(api, properties);

        if (api.Side == EnumAppSide.Client && flywheelShape != null)
        {
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

    public override float GetTorque(long tick, float speed, out float resistance)
    {
        float dt = GetDeltaTime(tick);
        if (!slipCoupled)
        {
            flywheelSpeed = speed;
            lastNetworkSpeed = speed;
            lastTransferTorque = 0f;
            lastTransferEnergyDelta = 0f;
            lastLossTorque = GetLossTorque(Math.Abs(speed));
            resistance = PassiveResistance + lastLossTorque;
            lastResistance = resistance;

            if (Api.Side == EnumAppSide.Server && tick % 10 == 0)
            {
                Blockentity.MarkDirty(false);
            }

            return 0f;
        }

        couplingEngagement = Math.Min(1f, couplingEngagement + dt / Math.Max(couplingRampSeconds, Epsilon));
        float transferTorque = GameMath.Clamp(couplingStrength * couplingEngagement * (flywheelSpeed - speed), -maxTransferTorque, maxTransferTorque);

        float beforeTransferSpeed = flywheelSpeed;
        ApplyFlywheelTorque(-transferTorque, dt);
        lastTransferEnergyDelta = Math.Abs(flywheelSpeed) - Math.Abs(beforeTransferSpeed);
        ApplyLosses(dt);

        bool drivesNetwork = Math.Abs(speed) < Epsilon
            ? Math.Abs(transferTorque) > Epsilon
            : transferTorque * speed > 0f;

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

        lastNetworkSpeed = speed;
        lastTransferTorque = transferTorque;
        lastResistance = resistance;

        if (Api.Side == EnumAppSide.Server && tick % 10 == 0)
        {
            Blockentity.MarkDirty(false);
        }

        return torque;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        flywheelSpeed = tree.GetFloat("flywheelSpeed");
        lastNetworkSpeed = tree.GetFloat("lastNetworkSpeed");
        lastTransferTorque = tree.GetFloat("lastTransferTorque");
        lastTransferEnergyDelta = tree.GetFloat("lastTransferEnergyDelta");
        lastResistance = tree.GetFloat("lastResistance");
        lastLossTorque = tree.GetFloat("lastLossTorque");
        base.FromTreeAttributes(tree, worldAccessForResolve);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("flywheelSpeed", flywheelSpeed);
        tree.SetFloat("lastNetworkSpeed", lastNetworkSpeed);
        tree.SetFloat("lastTransferTorque", lastTransferTorque);
        tree.SetFloat("lastTransferEnergyDelta", lastTransferEnergyDelta);
        tree.SetFloat("lastResistance", lastResistance);
        tree.SetFloat("lastLossTorque", lastLossTorque);
        base.ToTreeAttributes(tree);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);

        float displayFlywheelSpeed = GetDisplayFlywheelSpeed();
        float speedAbs = Math.Abs(displayFlywheelSpeed);
        float safeRatio = safeSpeed <= 0f ? 0f : GameMath.Clamp(speedAbs / safeSpeed, 0f, 1.5f);
        float storedPercent = GameMath.Clamp(safeRatio * safeRatio * 100f, 0f, 150f);
        float slip = Math.Abs(displayFlywheelSpeed - lastNetworkSpeed);

        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-state", GetStateLabel()));
        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-speed", Math.Round(displayFlywheelSpeed, 2), Math.Round(lastNetworkSpeed, 2)));
        sb.AppendLine(Lang.Get("flywheelpower:blockinfo-energy", Math.Round(storedPercent, 0)));
        string torqueKey = slipCoupled ? "flywheelpower:blockinfo-coupling" : "flywheelpower:blockinfo-shaft";
        sb.AppendLine(Lang.Get(torqueKey, Math.Round(lastTransferTorque, 3), Math.Round(lastResistance, 3)));

        if (slipCoupled && slip > 0.15f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-slipping"));
        }

        if (lastLossTorque > 0.02f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-losses", Math.Round(lastLossTorque, 3)));
        }

        if (safeRatio >= 0.9f)
        {
            sb.AppendLine(Lang.Get("flywheelpower:blockinfo-overspeed"));
        }
    }

    protected override CompositeShape GetShape()
    {
        return axleShape ?? base.GetShape();
    }

    public override void OnBlockRemoved()
    {
        RemoveFlywheelRenderable();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        RemoveFlywheelRenderable();
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
        }

        return Math.Min(0.25f, tickDelta * SecondsPerMechanicalTick);
    }

    private float ComputeVariantInertia(float referenceInertia)
    {
        string material = Blockentity.Block?.Variant?["material"];
        float densityFactor = GetMaterialDensity(material) / IronDensity;
        float outerRadius = Math.Max(wheelOuterRadius, Epsilon);
        float innerRadius = slipCoupled ? GameMath.Clamp(coupledInnerRadius, 0f, outerRadius - Epsilon) : 0f;
        float radiusFactor = (MathF.Pow(outerRadius, 4f) - MathF.Pow(innerRadius, 4f)) / MathF.Pow(FlywheelModelDimensions.WheelOuterRadius, 4f);
        float thicknessFactor = Math.Max(wheelHalfThickness, Epsilon) / FlywheelModelDimensions.WheelHalfThickness;

        // Mechanical-network torque units are game-tuned, so normalize the real cylinder/ring inertia formula against the original iron 3x3 value.
        return Math.Max(0.01f, referenceInertia * densityFactor * radiusFactor * thicknessFactor);
    }

    private static float GetMaterialDensity(string material)
    {
        return material switch
        {
            "wood" => 700f,
            "stone" => 2600f,
            "bronze" => 8800f,
            "meteoriciron" => 7800f,
            "steel" => 7850f,
            _ => IronDensity
        };
    }

    private void ApplyFlywheelTorque(float torque, float dt)
    {
        if (inertia <= 0f)
        {
            return;
        }

        flywheelSpeed += torque / inertia * dt;
        if (float.IsNaN(flywheelSpeed) || float.IsInfinity(flywheelSpeed))
        {
            flywheelSpeed = 0f;
        }
    }

    private void ApplyLosses(float dt)
    {
        float speedAbs = Math.Abs(flywheelSpeed);
        if (speedAbs < Epsilon)
        {
            flywheelSpeed = 0f;
            lastLossTorque = 0f;
            return;
        }

        float lossTorque = GetLossTorque(speedAbs);
        float speedLoss = lossTorque / Math.Max(inertia, Epsilon) * dt;
        flywheelSpeed = Math.Sign(flywheelSpeed) * Math.Max(0f, speedAbs - speedLoss);
        lastLossTorque = lossTorque;
    }

    private float GetLossTorque(float speedAbs)
    {
        if (speedAbs < Epsilon)
        {
            return 0f;
        }

        return baseBearingLoss + viscousBearingLoss * speedAbs + windageLoss * speedAbs * speedAbs;
    }

    private string GetStateLabel()
    {
        if (!slipCoupled)
        {
            return Math.Abs(GetDisplayFlywheelSpeed()) > 0.01f
                ? Lang.Get("flywheelpower:blockinfo-state-coasting")
                : Lang.Get("flywheelpower:blockinfo-state-idle");
        }

        if (lastTransferEnergyDelta > StateSpeedDeltaTolerance)
        {
            return Lang.Get("flywheelpower:blockinfo-state-charging");
        }

        if (lastTransferEnergyDelta < -StateSpeedDeltaTolerance)
        {
            return Lang.Get("flywheelpower:blockinfo-state-discharging");
        }

        if (Math.Abs(flywheelSpeed) > 0.01f)
        {
            return Lang.Get("flywheelpower:blockinfo-state-coasting");
        }

        return Lang.Get("flywheelpower:blockinfo-state-idle");
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
                AxisSign = new[] { -1, 0, 0 };
                OutFacingForNetworkDiscovery = BlockFacing.EAST;
                SetShapeRotation(0f, 0f, 0f);
                break;
            case "ud":
                AxisSign = new[] { 0, 1, 0 };
                OutFacingForNetworkDiscovery = BlockFacing.UP;
                SetShapeRotation(0f, 0f, 90f);
                break;
            default:
                AxisSign = new[] { 0, 0, -1 };
                OutFacingForNetworkDiscovery = BlockFacing.SOUTH;
                SetShapeRotation(0f, 90f, 0f);
                break;
        }
    }

    private void SetShapeRotation(float rotateX, float rotateY, float rotateZ)
    {
        SetShapeRotation(axleShape, rotateX, rotateY, rotateZ);
        SetShapeRotation(flywheelShape, rotateX, rotateY, rotateZ);
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

    private void RemoveFlywheelRenderable()
    {
        if (flywheelRenderable == null)
        {
            return;
        }

        manager?.RemoveDeviceForRender(flywheelRenderable);
        flywheelRenderable = null;
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
