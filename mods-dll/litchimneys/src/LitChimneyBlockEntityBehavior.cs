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
            var searchPos = new BlockPos
            {
                X = Pos.X,
                Y = height,
                Z = Pos.Z,
            };
            
            // Get the block entity and check if it implements IFirePit
            var blockEntity = Api.World.BlockAccessor.GetBlockEntity(searchPos);
            var foundFirepit = blockEntity as IFirePit;

            if (foundFirepit != null && foundFirepit.IsBurning)
            {
                return true;
            }

            var foundClayOven = Api.World.BlockAccessor.GetBlockEntity(searchPos) is BlockEntityOven { IsBurning: true };

            if (foundClayOven)
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