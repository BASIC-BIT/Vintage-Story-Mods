using System;
using System.Text;
using thebasics.ModSystems.Surgery.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.Surgery.Behaviors
{
    public class SurgicalTool : Item
    {
        public SurgicalToolDefinition ToolDefinition { get; private set; }
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            // Get the tool definition from attributes
            string toolCode = this.Attributes["surgicalToolCode"].AsString();
            if (api is ICoreServerAPI serverApi)
            {
                var toolRegistry = serverApi.ModLoader.GetModSystem<SurgeryModSystem>().ToolRegistry;
                ToolDefinition = toolRegistry.GetToolDefinition(toolCode);
                
                if (ToolDefinition == null)
                {
                    serverApi.Logger.Warning($"The BASICs: Item {this.Code} has surgicalToolCode {toolCode} but no matching tool definition was found");
                    // Create a default definition
                    ToolDefinition = new SurgicalToolDefinition
                    {
                        Code = toolCode,
                        Name = this.Code.ToString(),
                        UsesBeforeDegrading = 10,
                        QualityModifier = 1.0f
                    };
                }
                
                // Set quality modifier based on material
                UpdateQualityModifier();
            }
        }
        
        private void UpdateQualityModifier()
        {
            if (ToolDefinition == null) return;
            
            // Check item variant for material information
            var materialCategory = this.Variant?["material"]?.ToString().ToLowerInvariant();
            
            // Set quality modifier based on material
            if (!string.IsNullOrEmpty(materialCategory))
            {
                switch (materialCategory)
                {
                    case "stone":
                    case "flint":
                        ToolDefinition.QualityModifier = 0.6f; // 60% effectiveness
                        break;
                    case "copper":
                        ToolDefinition.QualityModifier = 0.8f; // 80% effectiveness
                        break;
                    case "bronzecopper":
                    case "bronze":
                    case "brass":
                    case "gold":
                    case "silver":
                        ToolDefinition.QualityModifier = 0.9f; // 90% effectiveness
                        break;
                    case "iron":
                        ToolDefinition.QualityModifier = 1.0f; // 100% effectiveness (standard)
                        break;
                    case "steel":
                    case "meteoriciron":
                        ToolDefinition.QualityModifier = 1.2f; // 120% effectiveness
                        break;
                    default:
                        ToolDefinition.QualityModifier = 1.0f;
                        break;
                }
            }
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
            if (player == null)
            {
                return;
            }
            
            // Try to perform a surgery step
            Entity targetEntity = entitySel.Entity;
            bool stepPerformed = SurgerySystem.Instance.PerformSurgeryStep(player as IServerPlayer, targetEntity, ToolDefinition.Code);
            
            if (stepPerformed)
            {
                // Mark as handled
                handling = EnumHandHandling.PreventDefault;
                
                // Degrade tool if needed
                if (ToolDefinition.Consumable || ShouldDegrade())
                {
                    DegradeTool(slot);
                }
            }
        }
        
        private bool ShouldDegrade()
        {
            // Randomize degradation based on uses
            Random random = new Random();
            return random.NextDouble() < (1.0 / ToolDefinition.UsesBeforeDegrading);
        }
        
        private void DegradeTool(ItemSlot slot)
        {
            if (ToolDefinition.Consumable)
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
        
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            
            if (ToolDefinition != null)
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("thebasics:tooltip-surgicaltool-use", ToolDefinition.Name));
                
                // Add quality information
                string qualityText;
                if (ToolDefinition.QualityModifier < 0.7f)
                    qualityText = "Poor";
                else if (ToolDefinition.QualityModifier < 0.9f)
                    qualityText = "Adequate";
                else if (ToolDefinition.QualityModifier < 1.1f)
                    qualityText = "Good";
                else
                    qualityText = "Excellent";
                    
                dsc.AppendLine(Lang.Get("thebasics:tooltip-surgicaltool-quality", qualityText));
            }
        }
    }
} 