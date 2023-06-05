using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace litchimneys;

public class LitChimneyBlockEntityBehavior : BlockEntityBehavior
{
    private long listenerId;

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        this.listenerId = api.Event.RegisterGameTickListener(Check, 3000);
        
        Api.Logger.Debug("LIT CHIMNEYS block initializing");
    }
    
    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        this.Api.World.UnregisterGameTickListener(listenerId);
    }

    private void Check(float dt)
    {
        var shouldBeLit = ShouldBeLit();
        var isLit = IsLit();

        Api.Logger.Debug($"LIT CHIMNEYS checking block..  shouldBeLit: {shouldBeLit}, isLit: {isLit}");
        if (shouldBeLit != isLit)
        {
            SetLit(shouldBeLit);
        }
    }

    private void SetLit(bool lit)
    {
        Block newBlock = Api.World.BlockAccessor.GetBlock(Block.CodeWithVariant("state", lit ? "lit" : "unlit"));

        Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, this.Pos);
    }

    private bool ShouldBeLit()
    {
        var height = Pos.Y;

        while (height > 0)
        {
            var foundFirepit = Api.World.BlockAccessor.GetInterface<IFirePit>(new BlockPos
            {
                X = Pos.X,
                Y = height,
                Z = Pos.Z,
            });
            
            Api.Logger.Debug("TEST");

            if (foundFirepit != null)
            {
                return true;
            }

            height--;
        }

        return false;
    }

    private bool IsLit()
    {
        return Block.Code.Path.Contains("-lit-");
    }

    public LitChimneyBlockEntityBehavior(BlockEntity blockentity) : base(blockentity)
    {
    }
}