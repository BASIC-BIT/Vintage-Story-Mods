using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Registry;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using System.Text;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.Surgery
{
    public class SurgeryModSystem : ModSystem
    {
        private ICoreServerAPI api;
        private SurgerySystem surgerySystem;
        private SurgicalToolRegistry toolRegistry;
        private BedSurgeryHandler bedSurgeryHandler;
        
        public SurgicalToolRegistry ToolRegistry => toolRegistry;
        
        /// <summary>
        /// Gets the surgery system instance
        /// </summary>
        public SurgerySystem SurgerySystem => surgerySystem;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            
            // Initialize tool registry first (needed by surgery system)
            toolRegistry = new SurgicalToolRegistry(api);
            toolRegistry.RegisterSurgicalTools();
            
            // Initialize surgery system
            surgerySystem = new SurgerySystem(api);
            
            // Initialize bed surgery handler
            bedSurgeryHandler = new BedSurgeryHandler(api, surgerySystem);
            
            // Register entity behaviors
            api.RegisterEntityBehaviorClass("medicalconditions", typeof(MedicalConditionBehavior));
            
            // Add medical condition behavior to entities with health
            api.Event.OnEntitySpawn += OnEntitySpawn;
            
            // Register additional commands
            RegisterCommands();
            
            api.Server.LogNotification("The BASICs: Surgery Mod System initialized");
        }
        
        private void OnEntitySpawn(Entity entity)
        {
            // Only add to entities with health
            if (entity.WatchedAttributes.HasAttribute("health"))
            {
                entity.AddBehavior(new MedicalConditionBehavior(entity));
            }
        }
        
        private void RegisterCommands()
        {
            // Create the main surgery command
            var cmd = api.ChatCommands
                .Create("surgery")
                .WithDescription("Surgery commands");
                
            // Handle the base surgery command
            cmd.HandleWith(args => {
                return TextCommandResult.Success("Available surgery commands: info, list, perform, heal, add, remove");
            });
                
            // Info subcommand
            cmd.BeginSubCommand("info")
                .WithDescription("Shows information about a player's or entity's medical conditions")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(OnSurgeryInfoCommand);
                
            // List subcommand
            cmd.BeginSubCommand("list")
                .WithDescription("Lists available surgery procedures")
                .HandleWith(OnSurgeryListCommand);
                
            // Perform subcommand
            cmd.BeginSubCommand("perform")
                .WithDescription("Performs a surgery procedure")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("procedureCode"),
                    api.ChatCommands.Parsers.OptionalWord("playerName")
                )
                .HandleWith(OnSurgeryPerformCommand);
                
            // Heal subcommand
            cmd.BeginSubCommand("heal")
                .WithDescription("Heals a player or entity's medical conditions")
                .WithArgs(
                    api.ChatCommands.Parsers.OptionalWord("playerName"),
                    api.ChatCommands.Parsers.OptionalWord("conditionCode")
                )
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnSurgeryHealCommand);
                
            // Add subcommand
            cmd.BeginSubCommand("add")
                .WithDescription("Adds a medical condition to a player or entity")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("conditionCode"),
                    api.ChatCommands.Parsers.OptionalWord("playerName"),
                    api.ChatCommands.Parsers.OptionalFloat("severity", 1.0f)
                )
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnSurgeryAddCommand);
                
            // Remove subcommand
            cmd.BeginSubCommand("remove")
                .WithDescription("Removes a medical condition from a player or entity")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("conditionCode"),
                    api.ChatCommands.Parsers.OptionalWord("playerName")
                )
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnSurgeryRemoveCommand);
        }
        
        private TextCommandResult OnSurgeryInfoCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            string playerName = args.Parsers[0].GetValue() as string;
            
            // Get the target player
            var targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                targetPlayer = api.World.PlayerByUid(playerName);
                if (targetPlayer == null)
                {
                    // Try to find by partial name
                    foreach (var connectedPlayer in api.World.AllPlayers)
                    {
                        if (connectedPlayer.PlayerName.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetPlayer = connectedPlayer;
                            break;
                        }
                    }
                    
                    if (targetPlayer == player) // Still the original player
                    {
                        return TextCommandResult.Error($"Player '{playerName}' not found");
                    }
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Get the medical conditions
            var medicalConditions = surgerySystem.GetEntityConditions(targetEntity);
            if (medicalConditions.Count == 0)
            {
                return TextCommandResult.Success($"{targetPlayer.PlayerName} has no medical conditions");
            }
            
            // Build the result
            var result = new StringBuilder();
            result.AppendLine($"{targetPlayer.PlayerName}'s medical conditions:");
            
            foreach (var condition in medicalConditions)
            {
                result.AppendLine($"- {condition.ConditionType}: {condition.Severity:P0} severity");
            }
            
            return TextCommandResult.Success(result.ToString());
        }
        
        private TextCommandResult OnSurgeryListCommand(TextCommandCallingArgs args)
        {
            var procedures = surgerySystem.GetAvailableProcedures();
            if (procedures.Count == 0)
            {
                return TextCommandResult.Success("No surgery procedures available");
            }
            
            // Build the result
            var result = new StringBuilder();
            result.AppendLine("Available surgery procedures:");
            
            foreach (var procedure in procedures)
            {
                result.AppendLine($"- {procedure.Name} (Code: {procedure.Code})");
                result.AppendLine($"  Treats: {string.Join(", ", procedure.ApplicableConditions)}");
                result.AppendLine($"  Steps: {procedure.Steps.Count}");
            }
            
            return TextCommandResult.Success(result.ToString());
        }
        
        private TextCommandResult OnSurgeryPerformCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string procedureCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers.Length > 1 ? args.Parsers[1].GetValue() as string : null;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                // Try to find by partial name
                foreach (var connectedPlayer in api.World.AllOnlinePlayers)
                {
                    if (connectedPlayer.PlayerName.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetPlayer = connectedPlayer as IServerPlayer;
                        break;
                    }
                }
                
                if (targetPlayer == player && playerName != player.PlayerName)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Get available body parts
            var bodyParts = surgerySystem.GetEntityBodyParts(targetEntity);
            if (bodyParts.Count == 0)
            {
                return TextCommandResult.Error("Target has no valid body parts for surgery");
            }
            
            // Default to first body part for now - could be improved with additional argument
            string bodyPartCode = bodyParts.First();
            
            // Attempt to perform the procedure
            bool success = surgerySystem.StartSurgery(player, targetEntity, bodyPartCode, procedureCode);
            if (!success)
            {
                return TextCommandResult.Error($"Failed to start procedure '{procedureCode}'");
            }
            
            return TextCommandResult.Success($"Started procedure '{procedureCode}' on {targetPlayer.PlayerName}");
        }
        
        private TextCommandResult OnSurgeryHealCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string playerName = args.Parsers.Length > 0 ? args.Parsers[0].GetValue() as string : null;
            string conditionCode = args.Parsers.Length > 1 ? args.Parsers[1].GetValue() as string : null;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                // Try to find by partial name
                foreach (var connectedPlayer in api.World.AllOnlinePlayers)
                {
                    if (connectedPlayer.PlayerName.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetPlayer = connectedPlayer as IServerPlayer;
                        break;
                    }
                }
                
                if (targetPlayer == player && playerName != player.PlayerName)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Get medical conditions
            var conditions = surgerySystem.GetEntityConditions(targetEntity);
            
            if (string.IsNullOrEmpty(conditionCode))
            {
                // Heal all conditions by removing them
                var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
                if (medicalBehavior != null)
                {
                    medicalBehavior.BodyPartConditions.Clear();
                    return TextCommandResult.Success($"Healed all medical conditions for {targetPlayer.PlayerName}");
                }
                return TextCommandResult.Error($"No medical conditions to heal for {targetPlayer.PlayerName}");
            }
            else
            {
                // Heal specific condition
                var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
                if (medicalBehavior != null)
                {
                    var condition = medicalBehavior.BodyPartConditions.FirstOrDefault(c => 
                        c.ConditionType.ToString().Equals(conditionCode, System.StringComparison.OrdinalIgnoreCase));
                    
                    if (condition != null)
                    {
                        medicalBehavior.BodyPartConditions.Remove(condition);
                        return TextCommandResult.Success($"Healed condition '{conditionCode}' for {targetPlayer.PlayerName}");
                    }
                }
                return TextCommandResult.Error($"Failed to heal condition '{conditionCode}' for {targetPlayer.PlayerName}");
            }
        }
        
        private TextCommandResult OnSurgeryAddCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string conditionCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers.Length > 1 ? args.Parsers[1].GetValue() as string : null;
            float severity = args.Parsers.Length > 2 ? (args.Parsers[2].GetValue() is float f ? f : 1.0f) : 1.0f;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                // Try to find by partial name
                foreach (var connectedPlayer in api.World.AllOnlinePlayers)
                {
                    if (connectedPlayer.PlayerName.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetPlayer = connectedPlayer as IServerPlayer;
                        break;
                    }
                }
                
                if (targetPlayer == player && playerName != player.PlayerName)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Try to parse the condition type
            if (!System.Enum.TryParse<MedicalConditionType>(conditionCode, true, out var conditionType))
            {
                return TextCommandResult.Error($"Unknown condition '{conditionCode}'");
            }
            
            // Add the condition
            var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error($"Entity doesn't support medical conditions");
            }
            
            // Get body parts and add to first body part for simplicity
            var bodyParts = surgerySystem.GetEntityBodyParts(targetEntity);
            if (bodyParts.Count == 0)
            {
                return TextCommandResult.Error("Entity has no valid body parts");
            }
            
            // Create new condition
            var condition = new BodyPartCondition
            {
                BodyPartCode = bodyParts.First(),
                ConditionType = conditionType,
                Severity = severity
            };
            
            medicalBehavior.BodyPartConditions.Add(condition);
            
            return TextCommandResult.Success($"Added condition '{conditionCode}' to {targetPlayer.PlayerName} with severity {severity:P0}");
        }
        
        private TextCommandResult OnSurgeryRemoveCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string conditionCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers.Length > 1 ? args.Parsers[1].GetValue() as string : null;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                // Try to find by partial name
                foreach (var connectedPlayer in api.World.AllOnlinePlayers)
                {
                    if (connectedPlayer.PlayerName.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetPlayer = connectedPlayer as IServerPlayer;
                        break;
                    }
                }
                
                if (targetPlayer == player && playerName != player.PlayerName)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Try to parse the condition type
            if (!System.Enum.TryParse<MedicalConditionType>(conditionCode, true, out var conditionType))
            {
                return TextCommandResult.Error($"Unknown condition '{conditionCode}'");
            }
            
            // Remove the condition
            var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error($"Entity doesn't support medical conditions");
            }
            
            // Find and remove matching conditions
            var matchingConditions = medicalBehavior.BodyPartConditions
                .Where(c => c.ConditionType == conditionType)
                .ToList();
                
            if (matchingConditions.Count == 0)
            {
                return TextCommandResult.Error($"No '{conditionCode}' condition found on {targetPlayer.PlayerName}");
            }
            
            foreach (var condition in matchingConditions)
            {
                medicalBehavior.BodyPartConditions.Remove(condition);
            }
            
            return TextCommandResult.Success($"Removed {matchingConditions.Count} '{conditionCode}' condition(s) from {targetPlayer.PlayerName}");
        }
        
        public override void Dispose()
        {
            if (api != null)
            {
                api.Event.OnEntitySpawn -= OnEntitySpawn;
            }
            
            base.Dispose();
        }
    }
} 