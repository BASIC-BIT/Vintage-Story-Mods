using System;
using System.Text;
using thebasics.ModSystems.Surgery.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.Surgery.Behaviors
{
    public class SurgicalToolBehavior : CollectibleBehavior
    {
        private readonly SurgicalToolDefinition toolDefinition;
        
        public SurgicalToolBehavior(CollectibleObject collObj, SurgicalToolDefinition toolDefinition) : base(collObj)
        {
            this.toolDefinition = toolDefinition;
        }
        
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // Only process first interaction
            if (!firstEvent) return;

            // Check if interaction is with an entity
            if (entitySel == null || !(byEntity.Api is ICoreServerAPI api))
            {
                return;
            }
            
            // Check if the player is performing surgery
            var player = api.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (player == null || !(player is IServerPlayer serverPlayer))
            {
                return;
            }
            
            // Try to perform a surgery step
            Entity targetEntity = entitySel.Entity;
            bool stepPerformed = SurgerySystem.Instance.PerformSurgeryStep(serverPlayer, targetEntity, toolDefinition.Code);
            
            if (stepPerformed)
            {
                // Mark as handled
                handling = EnumHandHandling.PreventDefault;
                
                // Degrade tool if needed
                if (toolDefinition.Consumable || ShouldDegrade())
                {
                    DegradeTool(slot);
                }
            }
        }
        
        private bool ShouldDegrade()
        {
            // Randomize degradation based on uses
            Random random = new Random();
            return random.NextDouble() < (1.0 / toolDefinition.UsesBeforeDegrading);
        }
        
        private void DegradeTool(ItemSlot slot)
        {
            if (toolDefinition.Consumable)
            {
                // Consume one item from stack
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            else
            {
                // Damage the tool
                var durability = slot.Itemstack.Attributes.GetInt("durability", 0);
                var maxDurability = slot.Itemstack.Collectible.Attributes?["durability"]?.AsInt(0) ?? 100;
                
                if (durability < maxDurability)
                {
                    durability++;
                    slot.Itemstack.Attributes.SetInt("durability", durability);
                    
                    // Break tool if max durability reached
                    if (durability >= maxDurability)
                    {
                        slot.Itemstack = null;
                    }
                    
                    slot.MarkDirty();
                }
            }
        }
        
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction {
                    ActionLangCode = "thebasics:interaction-surgicaltool-use",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak",
                    Itemstacks = null
                }
            };
        }
        
        public override void GetHeldInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine();
            dsc.AppendLine(world.Api.Lang.Translate("thebasics:tooltip-surgicaltool-use", toolDefinition.Name));
        }
    }
} 