using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Registry;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using System.Text;

namespace thebasics.ModSystems.Surgery
{
    public class SurgeryModSystem : ModSystem
    {
        private ICoreServerAPI api;
        private SurgerySystem surgerySystem;
        private SurgicalToolRegistry toolRegistry;
        
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
            CommandAbbr cmd = api.ChatCommands
                .Create("surgery")
                .WithDescription("Surgery commands");
                
            cmd.BeginSubCommand("info")
                .WithDescription("Shows information about a player's or entity's medical conditions")
                .WithArgs(api.ChatCommands.Parsers.WordRange("playerName", 0, 1))
                .HandleWith(OnSurgeryInfoCommand);
                
            cmd.BeginSubCommand("list")
                .WithDescription("Lists available surgery procedures")
                .HandleWith(OnSurgeryListCommand);
                
            cmd.BeginSubCommand("perform")
                .WithDescription("Performs a surgery procedure")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("procedureCode"),
                    api.ChatCommands.Parsers.OptionalWord("playerName")
                )
                .HandleWith(OnSurgeryPerformCommand);
                
            cmd.BeginSubCommand("heal")
                .WithDescription("Heals a player or entity's medical conditions")
                .WithArgs(
                    api.ChatCommands.Parsers.OptionalWord("playerName"),
                    api.ChatCommands.Parsers.OptionalWord("conditionCode")
                )
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnSurgeryHealCommand);
                
            cmd.BeginSubCommand("add")
                .WithDescription("Adds a medical condition to a player or entity")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("conditionCode"),
                    api.ChatCommands.Parsers.OptionalWord("playerName"),
                    api.ChatCommands.Parsers.OptionalFloat("severity", 1.0f)
                )
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnSurgeryAddCommand);
                
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
                targetPlayer = api.World.PlayerByName(playerName);
                if (targetPlayer == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Get the medical conditions
            var medicalConditions = surgerySystem.GetEntityMedicalConditions(targetEntity);
            if (medicalConditions.Count == 0)
            {
                return TextCommandResult.Success($"{targetPlayer.PlayerName} has no medical conditions");
            }
            
            // Build the result
            var result = new StringBuilder();
            result.AppendLine($"{targetPlayer.PlayerName}'s medical conditions:");
            
            foreach (var condition in medicalConditions)
            {
                result.AppendLine($"- {condition.Name}: {condition.Severity:P0} severity");
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
                result.AppendLine($"  Treats: {string.Join(", ", procedure.TreatedConditions)}");
                result.AppendLine($"  Steps: {procedure.Steps.Count}");
            }
            
            return TextCommandResult.Success(result.ToString());
        }
        
        private TextCommandResult OnSurgeryPerformCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string procedureCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers[1].GetValue() as string;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                targetPlayer = api.World.PlayerByName(playerName) as IServerPlayer;
                if (targetPlayer == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Attempt to perform the procedure
            bool success = surgerySystem.StartProcedure(player, targetEntity, procedureCode);
            if (!success)
            {
                return TextCommandResult.Error($"Failed to start procedure '{procedureCode}'");
            }
            
            return TextCommandResult.Success($"Started procedure '{procedureCode}' on {targetPlayer.PlayerName}");
        }
        
        private TextCommandResult OnSurgeryHealCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string playerName = args.Parsers[0].GetValue() as string;
            string conditionCode = args.Parsers[1].GetValue() as string;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                targetPlayer = api.World.PlayerByName(playerName) as IServerPlayer;
                if (targetPlayer == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Heal the condition
            if (string.IsNullOrEmpty(conditionCode))
            {
                // Heal all conditions
                surgerySystem.HealAllConditions(targetEntity);
                return TextCommandResult.Success($"Healed all medical conditions for {targetPlayer.PlayerName}");
            }
            else
            {
                // Heal specific condition
                bool success = surgerySystem.HealCondition(targetEntity, conditionCode);
                if (!success)
                {
                    return TextCommandResult.Error($"Failed to heal condition '{conditionCode}' for {targetPlayer.PlayerName}");
                }
                
                return TextCommandResult.Success($"Healed condition '{conditionCode}' for {targetPlayer.PlayerName}");
            }
        }
        
        private TextCommandResult OnSurgeryAddCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string conditionCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers[1].GetValue() as string;
            float severity = (float)args.Parsers[2].GetValue();
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                targetPlayer = api.World.PlayerByName(playerName) as IServerPlayer;
                if (targetPlayer == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Add the condition
            bool success = surgerySystem.AddCondition(targetEntity, conditionCode, severity);
            if (!success)
            {
                return TextCommandResult.Error($"Failed to add condition '{conditionCode}' to {targetPlayer.PlayerName}");
            }
            
            return TextCommandResult.Success($"Added condition '{conditionCode}' to {targetPlayer.PlayerName} with severity {severity:P0}");
        }
        
        private TextCommandResult OnSurgeryRemoveCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            string conditionCode = args.Parsers[0].GetValue() as string;
            string playerName = args.Parsers[1].GetValue() as string;
            
            // Get the target player
            IServerPlayer targetPlayer = player;
            if (!string.IsNullOrEmpty(playerName))
            {
                targetPlayer = api.World.PlayerByName(playerName) as IServerPlayer;
                if (targetPlayer == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found");
                }
            }
            
            // Get the target entity
            var targetEntity = targetPlayer.Entity;
            
            // Remove the condition
            bool success = surgerySystem.RemoveCondition(targetEntity, conditionCode);
            if (!success)
            {
                return TextCommandResult.Error($"Failed to remove condition '{conditionCode}' from {targetPlayer.PlayerName}");
            }
            
            return TextCommandResult.Success($"Removed condition '{conditionCode}' from {targetPlayer.PlayerName}");
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