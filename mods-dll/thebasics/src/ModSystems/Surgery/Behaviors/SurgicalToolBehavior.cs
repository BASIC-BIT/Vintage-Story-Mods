using System;
using System.Text;
using System.Collections.Generic;
using thebasics.ModSystems.Surgery.Models;
using thebasics.ModSystems.Surgery.Registry;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.Surgery.Behaviors
{
    public class SurgicalTool : Item
    {
        private SurgicalToolDefinition toolDefinition;
        private string toolCode;
        private int usesLeft;
        private string toolMaterial;
        private float qualityModifier = 1.0f;
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            // Get the tool definition
            toolCode = Attributes?["surgicalTool"]?.AsString() ?? Code.Path;
            
            // Get tool material from variant
            if (Variant.ContainsKey("material"))
            {
                toolMaterial = Variant["material"];
                
                // Get quality modifier from registry
                if (api is ICoreServerAPI srvApi)
                {
                    var modSystem = srvApi.ModLoader.GetModSystem<SurgeryModSystem>();
                    var registry = modSystem.ToolRegistry;
                    qualityModifier = registry.GetMaterialQualityModifier(toolMaterial);
                }
            }
            
            if (api is ICoreServerAPI sapi)
            {
                // Get the surgical tool definition from the registry
                var modSystem = sapi.ModLoader.GetModSystem<SurgeryModSystem>();
                var registry = modSystem.ToolRegistry;
                toolDefinition = registry.GetToolDefinition(toolCode);
                
                if (toolDefinition != null)
                {
                    // Initialize uses left if this is a new item
                    if (!Attributes.KeyExists("usesLeft"))
                    {
                        usesLeft = toolDefinition.UsesBeforeDegrading;
                        TreeAttribute tree = new TreeAttribute();
                        tree.SetInt("usesLeft", usesLeft);
                        Attributes["usesLeft"] = new IntAttribute(usesLeft);
                    }
                    else
                    {
                        usesLeft = Attributes["usesLeft"].AsInt();
                    }
                }
            }
        }
        
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            
            if (toolDefinition != null && !toolDefinition.Consumable)
            {
                // Display durability for non-consumable surgical tools
                dsc.AppendLine();
                
                if (toolMaterial != null)
                {
                    string qualityText = GetQualityText(qualityModifier);
                    dsc.AppendLine(Lang.Get("thebasics:surgicaltool-material-quality", toolMaterial, qualityText));
                }
                
                dsc.AppendLine(Lang.Get("thebasics:surgicaltool-durability", usesLeft));
            }
        }
        
        private string GetQualityText(float qualityModifier)
        {
            if (qualityModifier >= 1.2f) return Lang.Get("thebasics:surgicaltool-quality-excellent");
            if (qualityModifier >= 1.0f) return Lang.Get("thebasics:surgicaltool-quality-good");
            if (qualityModifier >= 0.8f) return Lang.Get("thebasics:surgicaltool-quality-decent");
            return Lang.Get("thebasics:surgicaltool-quality-poor");
        }
        
        /// <summary>
        /// Called when this tool is used in a surgery procedure
        /// </summary>
        /// <returns>True if the tool should be consumed</returns>
        public bool UseTool(IPlayer player)
        {
            if (toolDefinition == null)
                return false;
            
            if (toolDefinition.Consumable)
            {
                // Consumable items are used up completely
                return true;
            }
            else
            {
                // Reduce uses left
                usesLeft--;
                Attributes["usesLeft"] = new IntAttribute(usesLeft);
                
                // Mark as dirty to update client
                if (player?.Entity.World.Api is ICoreServerAPI serverApi)
                {
                    // Mark chunk as dirty to update clients
                    serverApi.World.BlockAccessor.MarkBlockDirty(player.Entity.Pos.AsBlockPos);
                }
                
                // Return true if the tool is broken
                if (usesLeft <= 0)
                {
                    if (player is IServerPlayer serverPlayer)
                    {
                        serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("thebasics:surgicaltool-broken", Variant["material"], GetHeldItemName(null)), EnumChatType.Notification);
                    }
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Get the quality modifier for this tool based on its material
        /// </summary>
        public float GetQualityModifier()
        {
            return qualityModifier;
        }
    }
} 