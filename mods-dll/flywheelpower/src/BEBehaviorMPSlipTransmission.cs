using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BEBehaviorMPSlipTransmission : BEBehaviorMPBase
{
    private static readonly float[] RatioOptions = { 0.5f, 1f, 2f };
    private const string SourceSection = "source";
    private const float SecondsPerMechanicalTick = 0.02f;
    private const float Epsilon = 0.00001f;

    private CompositeShape shaftShape;
    private float passiveResistance = 0.0005f;
    private float couplingStrength = 0.7f;
    private float maxTransferTorque = 0.45f;
    private float couplingRampSeconds = 0.75f;
    private float couplingEngagement;
    private float lastNetworkSpeed;
    private float lastTargetSpeed;
    private float lastTransferTorque;
    private float lastResistance;
    private long lastTorqueTick = -1;
    private int ratioIndex = 1;
    private bool engaged = true;

    public bool IsSource => Blockentity.Block?.Variant?["section"] == SourceSection;

    public BEBehaviorMPSlipTransmission(BlockEntity blockentity)
        : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        shaftShape = properties["shaftShape"].AsObject<CompositeShape>();
        passiveResistance = properties["passiveResistance"].AsFloat(passiveResistance);
        couplingStrength = properties["couplingStrength"].AsFloat(couplingStrength);
        maxTransferTorque = properties["maxTransferTorque"].AsFloat(maxTransferTorque);
        couplingRampSeconds = properties["couplingRampSeconds"].AsFloat(couplingRampSeconds);

        SetAxisAndShapeFromFacing();
        base.Initialize(api, properties);
    }

    public override void SetOrientations()
    {
        SetAxisAndShapeFromFacing();
    }

    public override float GetResistance()
    {
        return passiveResistance;
    }

    public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
    {
        return Array.Empty<MechPowerPath>();
    }

    public override float GetTorque(long tick, float speed, out float resistance)
    {
        float dt = GetDeltaTime(tick);
        lastNetworkSpeed = speed;

        if (!IsPairEngaged())
        {
            couplingEngagement = 0f;
            lastTargetSpeed = 0f;
            lastTransferTorque = 0f;
            resistance = passiveResistance;
            lastResistance = resistance;
            return 0f;
        }

        couplingEngagement = Math.Min(1f, couplingEngagement + dt / Math.Max(couplingRampSeconds, Epsilon));
        BEBehaviorMPSlipTransmission paired = GetPairedBehavior();
        if (paired == null)
        {
            lastTargetSpeed = 0f;
            lastTransferTorque = 0f;
            resistance = passiveResistance;
            lastResistance = resistance;
            return 0f;
        }

        float ratio = GetRatio();
        float pairedSpeed = paired.lastNetworkSpeed;
        lastTargetSpeed = IsSource ? pairedSpeed / Math.Max(ratio, Epsilon) : pairedSpeed * ratio;
        float desiredTorque = couplingStrength * couplingEngagement * (lastTargetSpeed - speed);
        float transferTorque = GameMath.Clamp(desiredTorque, -maxTransferTorque, maxTransferTorque);

        resistance = passiveResistance;
        float torque = 0f;
        if (Math.Abs(speed) < Epsilon || transferTorque * speed > 0f)
        {
            torque = transferTorque;
        }
        else
        {
            resistance += Math.Abs(transferTorque);
        }

        lastTransferTorque = transferTorque;
        lastResistance = resistance;

        if (Api.Side == EnumAppSide.Server && tick % 10 == 0)
        {
            Blockentity.MarkDirty(false);
        }

        return torque;
    }

    public float CycleRatio()
    {
        BEBehaviorMPSlipTransmission source = IsSource ? this : GetPairedBehavior();
        if (source == null)
        {
            return GetRatio();
        }

        source.ratioIndex = (source.ratioIndex + 1) % RatioOptions.Length;
        source.Blockentity.MarkDirty(false);
        BEBehaviorMPSlipTransmission paired = source.GetPairedBehavior();
        if (paired != null)
        {
            paired.ratioIndex = source.ratioIndex;
            paired.Blockentity.MarkDirty(false);
        }

        return source.GetRatio();
    }

    public BEBehaviorMPSlipTransmission GetPairedBehavior()
    {
        if (Api == null)
        {
            return null;
        }

        BlockFacing sourceFacing = GetSourceFacing();
        BlockPos pairedPos = IsSource ? Position.AddCopy(sourceFacing.Opposite) : Position.AddCopy(sourceFacing);
        return Api.World.BlockAccessor.GetBlockEntity(pairedPos)?.GetBehavior<BEBehaviorMPSlipTransmission>();
    }

    public void CheckEngaged()
    {
        BEBehaviorMPSlipTransmission source = IsSource ? this : GetPairedBehavior();
        source?.RefreshEngagementFromClutch();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        ratioIndex = GameMath.Mod(tree.GetInt("ratioIndex", ratioIndex), RatioOptions.Length);
        engaged = tree.GetBool("engaged", true);
        lastNetworkSpeed = tree.GetFloat("lastNetworkSpeed");
        lastTargetSpeed = tree.GetFloat("lastTargetSpeed");
        lastTransferTorque = tree.GetFloat("lastTransferTorque");
        lastResistance = tree.GetFloat("lastResistance");
        base.FromTreeAttributes(tree, worldAccessForResolve);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetInt("ratioIndex", ratioIndex);
        tree.SetBool("engaged", engaged);
        tree.SetFloat("lastNetworkSpeed", lastNetworkSpeed);
        tree.SetFloat("lastTargetSpeed", lastTargetSpeed);
        tree.SetFloat("lastTransferTorque", lastTransferTorque);
        tree.SetFloat("lastResistance", lastResistance);
        base.ToTreeAttributes(tree);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);
        sb.AppendLine(Lang.Get(IsPairEngaged() ? "flywheelpower:sliptransmission-info-engaged" : "flywheelpower:sliptransmission-info-disengaged"));
        sb.AppendLine(Lang.Get("flywheelpower:sliptransmission-info-ratio", GetRatio()));
        sb.AppendLine(Lang.Get("flywheelpower:sliptransmission-info-speed", Math.Round(lastNetworkSpeed, 2), Math.Round(lastTargetSpeed, 2)));
        sb.AppendLine(Lang.Get("flywheelpower:sliptransmission-info-transfer", Math.Round(lastTransferTorque, 3), Math.Round(lastResistance, 3)));
        sb.AppendLine(Lang.Get("flywheelpower:sliptransmission-info-adjust"));
    }

    protected override CompositeShape GetShape()
    {
        return shaftShape ?? base.GetShape();
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

    private bool IsPairEngaged()
    {
        BEBehaviorMPSlipTransmission source = IsSource ? this : GetPairedBehavior();
        if (source == null)
        {
            return engaged;
        }

        source.RefreshEngagementFromClutch();
        return source.engaged;
    }

    private void RefreshEngagementFromClutch()
    {
        bool newEngaged = GetClutchEngagement(out bool foundClutch);
        if (!foundClutch)
        {
            newEngaged = true;
        }

        if (engaged == newEngaged)
        {
            return;
        }

        engaged = newEngaged;
        couplingEngagement = 0f;
        Blockentity.MarkDirty(false);

        BEBehaviorMPSlipTransmission paired = GetPairedBehavior();
        if (paired != null)
        {
            paired.engaged = newEngaged;
            paired.couplingEngagement = 0f;
            paired.Blockentity.MarkDirty(false);
        }
    }

    private bool GetClutchEngagement(out bool foundClutch)
    {
        foundClutch = false;
        BlockPos pairedPos = GetPairedBehavior()?.Position;
        foreach (BlockFacing face in BlockFacing.ALLFACES)
        {
            if (TryGetClutchState(Position.AddCopy(face), Position, out bool clutchEngaged))
            {
                foundClutch = true;
                return clutchEngaged;
            }

            if (pairedPos != null && TryGetClutchState(pairedPos.AddCopy(face), pairedPos, out clutchEngaged))
            {
                foundClutch = true;
                return clutchEngaged;
            }
        }

        return true;
    }

    private bool TryGetClutchState(BlockPos clutchPos, BlockPos targetPos, out bool clutchEngaged)
    {
        clutchEngaged = true;
        if (Api.World.BlockAccessor.GetBlockEntity(clutchPos) is not BEClutch clutch)
        {
            return false;
        }

        if (!clutch.Position.AddCopy(clutch.Facing).Equals(targetPos))
        {
            return false;
        }

        clutchEngaged = clutch.Engaged;
        return true;
    }

    private float GetRatio()
    {
        if (!IsSource)
        {
            BEBehaviorMPSlipTransmission source = GetPairedBehavior();
            if (source != null)
            {
                return source.GetRatio();
            }
        }

        return RatioOptions[GameMath.Mod(ratioIndex, RatioOptions.Length)];
    }

    private void SetAxisAndShapeFromFacing()
    {
        BlockFacing externalFacing = GetExternalFacing();
        OutFacingForNetworkDiscovery = externalFacing;
        switch (externalFacing.Axis)
        {
            case EnumAxis.X:
                AxisSign = new[] { externalFacing == BlockFacing.EAST ? 1 : -1, 0, 0 };
                SetShapeRotation(0f, 0f, 0f);
                break;
            case EnumAxis.Y:
                AxisSign = new[] { 0, externalFacing == BlockFacing.UP ? 1 : -1, 0 };
                SetShapeRotation(0f, 0f, 90f);
                break;
            default:
                AxisSign = new[] { 0, 0, externalFacing == BlockFacing.SOUTH ? 1 : -1 };
                SetShapeRotation(0f, 90f, 0f);
                break;
        }
    }

    private void SetShapeRotation(float rotateX, float rotateY, float rotateZ)
    {
        if (shaftShape == null)
        {
            return;
        }

        shaftShape.rotateX = rotateX;
        shaftShape.rotateY = rotateY;
        shaftShape.rotateZ = rotateZ;
    }

    private BlockFacing GetExternalFacing()
    {
        BlockFacing sourceFacing = GetSourceFacing();
        return IsSource ? sourceFacing : sourceFacing.Opposite;
    }

    private BlockFacing GetSourceFacing()
    {
        return BlockFacing.FromCode(Blockentity.Block?.Variant?["facing"] ?? "north") ?? BlockFacing.NORTH;
    }
}
