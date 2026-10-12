using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Ropeway.Tests;

/// <summary>
/// The one part of cargo that is ours rather than vanilla's, and the one that could destroy a player's
/// goods. Everything else - the slots, the dialog, the two-level persistence in
/// <c>WatchedAttributes["wearablesInv"]</c> - is vanilla machinery the game itself is the only real test
/// of. Teardown is not: both of vanilla's drop paths are gated on <c>EnumDespawnReason.Death</c> and
/// <see cref="EntityRopewayCabin.DropAndDie"/> despawns with <c>Removed</c>, so without
/// <see cref="EntityRopewayCabin.UnloadCargo"/> every loaded basket on the cabin is deleted in silence.
/// </summary>
public class RopewayCargoTests
{
    /// <summary>
    /// A container that behaves like a vanilla basket for the two calls the drop path makes of it.
    /// <c>GetCollectibleInterface&lt;IHeldBag&gt;</c> checks <c>this is T</c> before it looks at behaviors
    /// (CollectibleObject.cs:3686-3697), so implementing the interface directly is enough.
    /// </summary>
    private sealed class Basket : Item, IHeldBag
    {
        public List<ItemStack> Goods = new();
        public bool Cleared;

        public bool IsEmpty(ItemStack bagstack) => Goods == null || Goods.Count == 0;
        public int GetQuantitySlots(ItemStack bagstack) => 8;

        // Null when the container has never been opened and so carries no `backpack` tree at all - which is
        // exactly what vanilla returns (CollectibleBehaviorHeldBag.cs:46-49), and the case that makes
        // Clear() unsafe to call unconditionally.
        public ItemStack[] GetContents(ItemStack bagstack, IWorldAccessor world) => Goods?.ToArray()!;
        public void Clear(ItemStack bagstack) { Goods.Clear(); Cleared = true; }
        public string GetSlotBgColor(ItemStack bagstack) => null!;
        public TagSet GetStorageTags(ItemStack bagStack) => default;

        public List<ItemSlotBagContent> GetOrCreateSlots(ItemStack bagstack, InventoryBase parentinv, int bagIndex, IWorldAccessor world)
            => throw new NotSupportedException();

        public void Store(ItemStack bagstack, ItemSlotBagContent slot) => throw new NotSupportedException();
    }

    [Fact]
    public void UnloadingABenchHandsBackTheGoodsThenTheContainerAndLeavesTheSlotEmpty()
    {
        var basket = new Basket();
        var ore = new ItemStack(new Item(), 12);
        basket.Goods.Add(ore);

        var container = new ItemStack(basket);
        var loaded = new DummySlot(container);
        var handed = new List<ItemStack>();

        var emptied = EntityRopewayCabin.UnloadCargo(new ItemSlot[] { loaded, new DummySlot() }, null, handed.Add);

        Assert.Equal(1, emptied);

        // Goods first, container second. The order is the point: a container itemstack still carrying a
        // `backpack` tree loses it the moment the block is placed (BlockEntityGenericTypedContainer
        // .OnBlockPlaced reads only type/isPerPlayer, then calls base.OnBlockPlaced(null)), so handing back
        // a loaded basket is handing back a basket that eats its own contents. Cargo spills instead.
        Assert.Equal(new[] { ore, container }, handed);
        Assert.True(basket.Cleared, "the basket was handed back still holding its goods");
        Assert.True(loaded.Empty, "the bench still holds a container the cabin is about to despawn with");
    }

    [Fact]
    public void UnloadingHandsBackAContainerlessStackRatherThanSkippingIt()
    {
        // A stack with no IHeldBag - a basket variant that lost the behavior, an item some other mod put
        // there. It has nothing inside it to spill, but it is still someone's block and must come back.
        var stack = new ItemStack(new Item());
        var slot = new DummySlot(stack);
        var handed = new List<ItemStack>();

        Assert.Equal(1, EntityRopewayCabin.UnloadCargo(new ItemSlot[] { slot }, null, handed.Add));
        Assert.Equal(new[] { stack }, handed);
        Assert.True(slot.Empty);
    }

    [Fact]
    public void UnloadingANeverOpenedContainerHandsItBackWithoutTouchingItsMissingBackpackTree()
    {
        // Attach a basket, never open it, then break the line. It has no `backpack` tree, so GetContents
        // is null and Clear() would dereference nothing - the crash that would take the whole teardown
        // down with it, cabin item included.
        var basket = new Basket { Goods = null! };
        var container = new ItemStack(basket);
        var slot = new DummySlot(container);
        var handed = new List<ItemStack>();

        Assert.Equal(1, EntityRopewayCabin.UnloadCargo(new ItemSlot[] { slot }, null, handed.Add));
        Assert.Equal(new[] { container }, handed);
        Assert.False(basket.Cleared);
        Assert.True(slot.Empty);
    }

    [Fact]
    public void UnloadingAnEmptyOrAbsentInventoryHandsBackNothing()
    {
        var handed = new List<ItemStack>();

        Assert.Equal(0, EntityRopewayCabin.UnloadCargo(null, null, handed.Add));
        Assert.Equal(0, EntityRopewayCabin.UnloadCargo(new ItemSlot[] { new DummySlot(), new DummySlot() }, null, handed.Add));
        Assert.Empty(handed);
    }
}
