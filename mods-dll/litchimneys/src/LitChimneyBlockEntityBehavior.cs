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
        
        if (shouldBeLit != isLit)
        {
            SetLit(shouldBeLit);
        }
    }

    private void SetLit(bool lit)
    {
        Block newBlock = Api.World.BlockAccessor.GetBlock(Block.CodeWithVariant("state", lit ? "lit" : "unlit"));

        Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, this.Pos);
        
        //Fix particles because of new block
        Blockentity.Initialize(Api);
    }

    private bool ShouldBeLit()
    {
        var height = Pos.Copy().Y;

        while (height > 0)
        {
            var foundFirepit = Api.World.BlockAccessor.GetInterface<IFirePit>(new BlockPos
            {
                X = Pos.X,
                Y = height,
                Z = Pos.Z,
            });

            if (foundFirepit != null && foundFirepit.IsBurning)
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