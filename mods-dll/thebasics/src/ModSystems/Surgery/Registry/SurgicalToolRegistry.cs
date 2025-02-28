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
            
            // Register the SurgicalTool class
            api.RegisterItemClass("SurgicalTool", typeof(SurgicalTool));
            
            api.Server.LogNotification($"The BASICs: Registered {defaultTools.Count} surgical tool definitions");
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
} 