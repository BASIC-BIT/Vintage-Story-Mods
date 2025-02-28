using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Registry;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.Surgery
{
    public class SurgeryModSystem : ModSystem
    {
        private ICoreServerAPI api;
        private SurgerySystem surgerySystem;
        private SurgicalToolRegistry toolRegistry;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            
            // Initialize surgery system
            surgerySystem = new SurgerySystem(api);
            
            // Register entity behaviors
            api.RegisterEntityBehaviorClass("medicalconditions", typeof(MedicalConditionBehavior));
            
            // Add medical condition behavior to entities with health
            api.Event.OnEntitySpawn += OnEntitySpawn;
            
            // Initialize tool registry
            toolRegistry = new SurgicalToolRegistry(api);
            toolRegistry.RegisterSurgicalTools();
            
            // Register additional commands
            RegisterCommands();
            
            api.Server.LogNotification("The BASICs: Surgery Mod System initialized");
        }
        
        private void OnEntitySpawn(Entity entity)
        {
            // Only add to entities with health
            if (entity.WatchedAttributes.HasAttribute("health"))
            {
                // Skip if already has the behavior
                if (entity.GetBehavior<MedicalConditionBehavior>() == null)
                {
                    entity.AddBehavior(new MedicalConditionBehavior(entity));
                }
            }
        }
        
        private void RegisterCommands()
        {
            // Command to cause bleeding for testing
            api.ChatCommands.Create("bleed")
                .WithDescription("Causes an entity to bleed for testing")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(HandleBleedCommand);
            
            // Command to cause infection for testing
            api.ChatCommands.Create("infect")
                .WithDescription("Causes an entity to be infected for testing")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(HandleInfectCommand);
            
            // Command to damage a specific body part
            api.ChatCommands.Create("damagebody")
                .WithDescription("Damages a body part on an entity")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(HandleDamageBodyCommand);
            
            // Command to heal a specific body part
            api.ChatCommands.Create("healbody")
                .WithDescription("Heals a body part on an entity")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(HandleHealBodyCommand);
            
            // Command to start surgery
            api.ChatCommands.Create("surgery")
                .WithDescription("Starts a surgical procedure")
                .RequiresPlayer()
                .HandleWith(HandleSurgeryCommand);
            
            // Command to cancel surgery
            api.ChatCommands.Create("cancelsurgery")
                .WithDescription("Cancels an active surgical procedure")
                .RequiresPlayer()
                .HandleWith(HandleCancelSurgeryCommand);
        }
        
        private TextCommandResult HandleBleedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            var entity = player.CurrentEntitySelection.Entity;
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error("Target entity doesn't support medical conditions");
            }
            
            float rate = 1.0f;
            if (args.ArgCount > 0 && float.TryParse(args[0].ToString(), out float parsedRate))
            {
                rate = parsedRate;
            }
            
            medicalBehavior.SetBleeding(true, rate);
            return TextCommandResult.Success($"Made {entity.GetName()} bleed at rate {rate}");
        }
        
        private TextCommandResult HandleInfectCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            var entity = player.CurrentEntitySelection.Entity;
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error("Target entity doesn't support medical conditions");
            }
            
            float level = 1.0f;
            if (args.ArgCount > 0 && float.TryParse(args[0].ToString(), out float parsedLevel))
            {
                level = parsedLevel;
            }
            
            medicalBehavior.SetInfection(true, level);
            return TextCommandResult.Success($"Infected {entity.GetName()} at level {level}");
        }
        
        private TextCommandResult HandleDamageBodyCommand(TextCommandCallingArgs args)
        {
            if (args.ArgCount < 1)
            {
                return TextCommandResult.Error("Usage: /damagebody <bodypart> [amount] [bleed] [infect]");
            }
            
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            var entity = player.CurrentEntitySelection.Entity;
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error("Target entity doesn't support medical conditions");
            }
            
            string bodyPartCode = args[0].ToString();
            float amount = args.ArgCount > 1 ? float.Parse(args[1].ToString()) : 1.0f;
            bool bleed = args.ArgCount > 2 && bool.Parse(args[2].ToString());
            bool infect = args.ArgCount > 3 && bool.Parse(args[3].ToString());
            
            medicalBehavior.DamageBodyPart(bodyPartCode, amount, bleed, infect);
            return TextCommandResult.Success($"Damaged {entity.GetName()}'s {bodyPartCode} by {amount}");
        }
        
        private TextCommandResult HandleHealBodyCommand(TextCommandCallingArgs args)
        {
            if (args.ArgCount < 1)
            {
                return TextCommandResult.Error("Usage: /healbody <bodypart> [amount]");
            }
            
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            var entity = player.CurrentEntitySelection.Entity;
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            
            if (medicalBehavior == null)
            {
                return TextCommandResult.Error("Target entity doesn't support medical conditions");
            }
            
            string bodyPartCode = args[0].ToString();
            float amount = args.ArgCount > 1 ? float.Parse(args[1].ToString()) : 1.0f;
            
            medicalBehavior.HealBodyPart(bodyPartCode, amount);
            return TextCommandResult.Success($"Healed {entity.GetName()}'s {bodyPartCode} by {amount}");
        }
        
        private TextCommandResult HandleSurgeryCommand(TextCommandCallingArgs args)
        {
            if (args.ArgCount < 2)
            {
                return TextCommandResult.Error("Usage: /surgery <bodypart> <procedure>");
            }
            
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            string bodyPartCode = args[0].ToString();
            string procedureCode = args[1].ToString();
            
            bool success = surgerySystem.StartSurgery(player, player.CurrentEntitySelection.Entity, bodyPartCode, procedureCode);
            
            if (success)
            {
                return TextCommandResult.Success("Surgery started. Use surgical tools to perform the procedure.");
            }
            else
            {
                return TextCommandResult.Error("Failed to start surgery. Check if the body part and procedure are valid.");
            }
        }
        
        private TextCommandResult HandleCancelSurgeryCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player.CurrentEntitySelection == null)
            {
                return TextCommandResult.Error("You must be looking at an entity to use this command");
            }
            
            surgerySystem.CancelSurgery(player, player.CurrentEntitySelection.Entity);
            return TextCommandResult.Success("Surgery cancelled.");
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