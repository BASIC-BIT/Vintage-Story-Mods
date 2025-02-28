using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Surgery.Registry
{
    public class SurgicalToolRegistry
    {
        private readonly ICoreServerAPI api;
        private readonly Dictionary<string, SurgicalToolDefinition> toolDefinitions = new Dictionary<string, SurgicalToolDefinition>();
        private Dictionary<string, float> materialQualityModifiers = new Dictionary<string, float>();
        private Dictionary<string, int> toolUsesBeforeDegrading = new Dictionary<string, int>();
        
        public SurgicalToolRegistry(ICoreServerAPI api)
        {
            this.api = api;
            
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
            // Create default tool definitions
            var defaultTools = CreateDefaultToolDefinitions();
            
            // Register tool definitions
            foreach (var tool in defaultTools)
            {
                toolDefinitions[tool.Code] = tool;
            }
            
            // Register the SurgicalTool class
            api.RegisterItemClass("SurgicalTool", typeof(SurgicalTool));
            
            api.Server.LogNotification($"The BASICs: Registered {defaultTools.Count} surgical tool definitions");
        }
        
        private List<SurgicalToolDefinition> CreateDefaultToolDefinitions()
        {
            var tools = new List<SurgicalToolDefinition>();
            
            // Add all tools with their configurations
            foreach (var toolPair in toolUsesBeforeDegrading)
            {
                string toolCode = toolPair.Key;
                int uses = toolPair.Value;
                bool consumable = uses <= 1;
                
                tools.Add(new SurgicalToolDefinition
                {
                    Code = toolCode,
                    Name = char.ToUpper(toolCode[0]) + toolCode.Substring(1).Replace('_', ' '),
                    UsesBeforeDegrading = uses,
                    Consumable = consumable
                });
            }
            
            return tools;
        }
        
        public SurgicalToolDefinition GetToolDefinition(string code)
        {
            return toolDefinitions.TryGetValue(code, out var definition) ? definition : null;
        }
        
        public float GetMaterialQualityModifier(string material)
        {
            return materialQualityModifiers.TryGetValue(material.ToLowerInvariant(), out float modifier) ? modifier : 1.0f;
        }
    }
} 