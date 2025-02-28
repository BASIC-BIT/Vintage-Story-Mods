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
        
        public SurgicalToolRegistry(ICoreServerAPI api)
        {
            this.api = api;
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
            
            // Register collectible behaviors for surgical tools
            api.RegisterCollectibleBehaviorClass("SurgicalToolBehavior", typeof(SurgicalToolBehavior));
            
            api.Event.CollectibleLoaded += OnCollectibleLoaded;
        }
        
        private void OnCollectibleLoaded(CollectibleObject collectible)
        {
            // Check if collectible is a surgical tool
            if (collectible.Attributes?["surgicalTool"]?.AsBool() == true)
            {
                string toolCode = collectible.Attributes["surgicalToolCode"].AsString();
                if (string.IsNullOrEmpty(toolCode) || !toolDefinitions.TryGetValue(toolCode, out var toolDef))
                {
                    api.Server.LogWarning($"The BASICs: Item {collectible.Code} has surgicalTool attribute but no valid surgicalToolCode");
                    return;
                }
                
                // Add surgical tool behavior to the collectible
                collectible.CollectibleBehaviors = collectible.CollectibleBehaviors.Append(
                    new SurgicalToolBehavior(collectible, toolDef)
                ).ToArray();
                
                api.Server.LogNotification($"The BASICs: Registered surgical tool {toolDef.Name} for item {collectible.Code}");
            }
        }
        
        private List<SurgicalToolDefinition> CreateDefaultToolDefinitions()
        {
            return new List<SurgicalToolDefinition>
            {
                new SurgicalToolDefinition
                {
                    Code = "scalpel",
                    Name = "Scalpel",
                    UsesBeforeDegrading = 15
                },
                new SurgicalToolDefinition
                {
                    Code = "forceps",
                    Name = "Forceps",
                    UsesBeforeDegrading = 20
                },
                new SurgicalToolDefinition
                {
                    Code = "suture",
                    Name = "Suture",
                    Consumable = true
                },
                new SurgicalToolDefinition
                {
                    Code = "bandage",
                    Name = "Bandage",
                    Consumable = true
                },
                new SurgicalToolDefinition
                {
                    Code = "bone_saw",
                    Name = "Bone Saw",
                    UsesBeforeDegrading = 15
                },
                new SurgicalToolDefinition
                {
                    Code = "splint",
                    Name = "Splint",
                    Consumable = true
                },
                new SurgicalToolDefinition
                {
                    Code = "disinfectant",
                    Name = "Disinfectant",
                    Consumable = true
                }
            };
        }
        
        public SurgicalToolDefinition GetToolDefinition(string code)
        {
            return toolDefinitions.TryGetValue(code, out var definition) ? definition : null;
        }
    }
    
    public class SurgicalItemClass : Item
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            // Add surgical tool attribute if not present
            if (this.Attributes?["surgicalTool"] == null)
            {
                this.Attributes.SetBool("surgicalTool", true);
            }
        }
    }
} 