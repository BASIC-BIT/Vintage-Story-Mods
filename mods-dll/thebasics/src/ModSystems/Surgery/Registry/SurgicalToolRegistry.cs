using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Models;
using thebasics.ModSystems.Surgery.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Surgery.Registry
{
    public class SurgicalToolRegistry
    {
        private ICoreServerAPI api;
        private Dictionary<string, SurgicalToolDefinition> toolDefinitions;
        private Dictionary<string, float> materialQualityModifiers = new Dictionary<string, float>();
        private Dictionary<string, int> toolUsesBeforeDegrading = new Dictionary<string, int>();
        
        public SurgicalToolRegistry(ICoreServerAPI api)
        {
            this.api = api;
            this.toolDefinitions = new Dictionary<string, SurgicalToolDefinition>();
            
            // Load material quality modifiers from config
            LoadToolConfigurations();
        }
        
        private void LoadToolConfigurations()
        {
            try
            {
                // Default values
                materialQualityModifiers = new Dictionary<string, float>
                {
                    { "flint", 0.6f },
                    { "stone", 0.6f },
                    { "copper", 0.8f },
                    { "bronze", 0.9f },
                    { "brass", 0.9f },
                    { "gold", 0.9f },
                    { "silver", 0.9f },
                    { "iron", 1.0f },
                    { "steel", 1.2f },
                    { "meteoriciron", 1.2f }
                };
                
                toolUsesBeforeDegrading = new Dictionary<string, int>
                {
                    { "scalpel", 15 },
                    { "forceps", 20 },
                    { "bone_saw", 15 },
                    { "suture", 1 },
                    { "bandage", 1 },
                    { "splint", 1 },
                    { "disinfectant", 1 }
                };
                
                // Try to load from file
                string configPath = "thebasics/config/surgery.json";
                var configAsset = api.Assets.TryGet(configPath);
                if (configAsset != null)
                {
                    var configObj = configAsset.ToObject<Dictionary<string, object>>();
                    
                    // Get tool settings
                    if (configObj.TryGetValue("ToolSettings", out object toolSettingsObj) && 
                        toolSettingsObj is Dictionary<string, object> toolSettings)
                    {
                        // Get material quality modifiers
                        if (toolSettings.TryGetValue("MaterialQualityModifiers", out object materialModifiersObj) && 
                            materialModifiersObj is Dictionary<string, object> materialModifiers)
                        {
                            foreach (var pair in materialModifiers)
                            {
                                materialQualityModifiers[pair.Key] = Convert.ToSingle(pair.Value);
                            }
                        }
                        
                        // Get uses before degrading
                        if (toolSettings.TryGetValue("UsesBeforeDegrading", out object usesObj) && 
                            usesObj is Dictionary<string, object> uses)
                        {
                            foreach (var pair in uses)
                            {
                                toolUsesBeforeDegrading[pair.Key] = Convert.ToInt32(pair.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"Error loading tool configurations: {ex.Message}");
            }
        }
        
        public void RegisterSurgicalTools()
        {
            // Register all tool types
            RegisterTool("scalpel", 1.0f);
            RegisterTool("forceps", 0.9f);
            RegisterTool("retractor", 0.85f);
            RegisterTool("needle", 0.8f);
            RegisterTool("saw", 0.7f);
            RegisterTool("scissors", 0.9f);
            
            // Register the SurgicalTool class
            api.RegisterItemClass("SurgicalTool", typeof(Behaviors.SurgicalTool));
            
            api.Logger.Notification($"SurgicalToolRegistry: Registered {toolDefinitions.Count} surgical tools");
        }
        
        private void RegisterTool(string code, float qualityModifier)
        {
            toolDefinitions[code] = new SurgicalToolDefinition
            {
                Code = code,
                QualityModifier = qualityModifier
            };
        }
        
        public SurgicalToolDefinition GetToolDefinition(string toolCode)
        {
            if (toolDefinitions.TryGetValue(toolCode, out SurgicalToolDefinition toolDef))
            {
                return toolDef;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the surgical tool code from an ItemStack, or null if not a surgical tool
        /// </summary>
        public string GetToolCode(ItemStack itemStack)
        {
            if (itemStack == null)
                return null;
                
            // Check if the item is a surgical tool
            if (itemStack.Collectible is Behaviors.SurgicalTool surgicalTool)
            {
                // Use the toolCode field or method from the existing implementation
                return itemStack.Attributes?.HasAttribute("surgicalTool") == true 
                    ? itemStack.Attributes.GetString("surgicalTool") 
                    : itemStack.Collectible.Code.Path;
            }
            
            // Alternative method: Check item attributes
            if (itemStack.Attributes != null && itemStack.Attributes.HasAttribute("surgicalTool"))
            {
                return itemStack.Attributes.GetString("surgicalTool");
            }
            
            // Check item class - if it's a SurgicalTool but didn't match above
            if (itemStack.Collectible.GetType().Name == "SurgicalTool")
            {
                // Try to determine tool type from the item code
                string itemCode = itemStack.Collectible.Code.Path;
                
                foreach (var toolDef in toolDefinitions.Keys)
                {
                    if (itemCode.Contains(toolDef))
                    {
                        return toolDef;
                    }
                }
            }
            
            // Not a surgical tool
            return null;
        }
        
        /// <summary>
        /// Gets the quality modifier for a tool material
        /// </summary>
        public float GetMaterialQualityModifier(string material)
        {
            // Define quality modifiers for different materials
            switch (material?.ToLowerInvariant())
            {
                case "flint": return 0.7f;
                case "copper": return 0.8f;
                case "bronze": return 0.9f;
                case "iron": return 1.0f;
                case "steel": return 1.1f;
                case "meteoriciron": return 1.2f;
                default: return 1.0f; // Default quality
            }
        }
    }
    
    public class SurgicalToolDefinition
    {
        public string Code { get; set; }
        public float QualityModifier { get; set; }
        public int UsesBeforeDegrading { get; set; } = 10; // Default durability
        public bool Consumable { get; set; } = false;
    }
} 