using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Registry;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

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
            api.RegisterCommand("bleed", "Causes an entity to bleed for testing", "[rate]", (player, groupId, args) =>
            {
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                var entity = player.CurrentEntitySelection.Entity;
                var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
                
                if (medicalBehavior == null)
                {
                    player.SendMessage(groupId, "Target entity doesn't support medical conditions", EnumChatType.CommandError);
                    return;
                }
                
                float rate = 1.0f;
                if (args.Length > 0)
                {
                    float.TryParse(args[0], out rate);
                }
                
                medicalBehavior.SetBleeding(true, rate);
                player.SendMessage(groupId, $"Made {entity.GetName()} bleed at rate {rate}", EnumChatType.CommandSuccess);
            });
            
            // Command to cause infection for testing
            api.RegisterCommand("infect", "Causes an entity to be infected for testing", "[level]", (player, groupId, args) =>
            {
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                var entity = player.CurrentEntitySelection.Entity;
                var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
                
                if (medicalBehavior == null)
                {
                    player.SendMessage(groupId, "Target entity doesn't support medical conditions", EnumChatType.CommandError);
                    return;
                }
                
                float level = 1.0f;
                if (args.Length > 0)
                {
                    float.TryParse(args[0], out level);
                }
                
                medicalBehavior.SetInfection(true, level);
                player.SendMessage(groupId, $"Infected {entity.GetName()} at level {level}", EnumChatType.CommandSuccess);
            });
            
            // Command to damage a specific body part
            api.RegisterCommand("damagebody", "Damages a body part on an entity", "<bodypart> [amount] [bleed] [infect]", (player, groupId, args) =>
            {
                if (args.Length < 1)
                {
                    player.SendMessage(groupId, "Usage: /damagebody <bodypart> [amount] [bleed] [infect]", EnumChatType.CommandError);
                    return;
                }
                
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                var entity = player.CurrentEntitySelection.Entity;
                var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
                
                if (medicalBehavior == null)
                {
                    player.SendMessage(groupId, "Target entity doesn't support medical conditions", EnumChatType.CommandError);
                    return;
                }
                
                string bodyPartCode = args[0];
                float amount = args.Length > 1 ? float.Parse(args[1]) : 1.0f;
                bool bleed = args.Length > 2 && bool.Parse(args[2]);
                bool infect = args.Length > 3 && bool.Parse(args[3]);
                
                medicalBehavior.DamageBodyPart(bodyPartCode, amount, bleed, infect);
                player.SendMessage(groupId, $"Damaged {entity.GetName()}'s {bodyPartCode} by {amount}", EnumChatType.CommandSuccess);
            });
            
            // Command to heal a specific body part
            api.RegisterCommand("healbody", "Heals a body part on an entity", "<bodypart> [amount]", (player, groupId, args) =>
            {
                if (args.Length < 1)
                {
                    player.SendMessage(groupId, "Usage: /healbody <bodypart> [amount]", EnumChatType.CommandError);
                    return;
                }
                
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                var entity = player.CurrentEntitySelection.Entity;
                var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
                
                if (medicalBehavior == null)
                {
                    player.SendMessage(groupId, "Target entity doesn't support medical conditions", EnumChatType.CommandError);
                    return;
                }
                
                string bodyPartCode = args[0];
                float amount = args.Length > 1 ? float.Parse(args[1]) : 1.0f;
                
                medicalBehavior.HealBodyPart(bodyPartCode, amount);
                player.SendMessage(groupId, $"Healed {entity.GetName()}'s {bodyPartCode} by {amount}", EnumChatType.CommandSuccess);
            });
            
            // Command to start surgery
            api.RegisterCommand("surgery", "Starts a surgical procedure", "<bodypart> <procedure>", (player, groupId, args) =>
            {
                if (args.Length < 2)
                {
                    player.SendMessage(groupId, "Usage: /surgery <bodypart> <procedure>", EnumChatType.CommandError);
                    return;
                }
                
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                string bodyPartCode = args[0];
                string procedureCode = args[1];
                
                bool success = surgerySystem.StartSurgery(player, player.CurrentEntitySelection.Entity, bodyPartCode, procedureCode);
                
                if (success)
                {
                    player.SendMessage(groupId, "Surgery started. Use surgical tools to perform the procedure.", EnumChatType.CommandSuccess);
                }
            });
            
            // Command to cancel surgery
            api.RegisterCommand("cancelsurgery", "Cancels an active surgical procedure", "", (player, groupId, args) =>
            {
                if (player.CurrentEntitySelection == null)
                {
                    player.SendMessage(groupId, "You must be looking at an entity to use this command", EnumChatType.CommandError);
                    return;
                }
                
                surgerySystem.CancelSurgery(player, player.CurrentEntitySelection.Entity);
                player.SendMessage(groupId, "Surgery cancelled.", EnumChatType.CommandSuccess);
            });
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