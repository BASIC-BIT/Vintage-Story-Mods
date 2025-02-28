using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using thebasics.ModSystems.Surgery.Behaviors;
using thebasics.ModSystems.Surgery.Models;
using thebasics.ModSystems.Surgery.Registry;

namespace thebasics.ModSystems.Surgery
{
    // Define GlobalConstants needed for chat
    public static class GlobalConstants
    {
        public const int GeneralChatGroup = 0;
    }

    public class SurgerySystem
    {
        public static SurgerySystem Instance { get; private set; }
        
        private readonly ICoreAPI api;
        private readonly Dictionary<string, BodyPartDefinition> bodyPartRegistry = new Dictionary<string, BodyPartDefinition>();
        private readonly Dictionary<string, SurgicalProcedureDefinition> procedureRegistry = new Dictionary<string, SurgicalProcedureDefinition>();
        private readonly Dictionary<string, List<string>> entityBodyParts = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, Dictionary<string, SurgeryProcedureState>> activeSurgeries = new Dictionary<string, Dictionary<string, SurgeryProcedureState>>();
        
        public SurgerySystem(ICoreAPI api)
        {
            this.api = api;
            Instance = this;
            
            // Load definitions from configuration
            LoadBodyPartDefinitions();
            LoadProcedureDefinitions();
            MapEntityBodyParts();
        }
        
        private void LoadBodyPartDefinitions()
        {
            // For now, hardcode some definitions
            // Eventually these would be loaded from a JSON config file
            List<BodyPartDefinition> bodyParts = new List<BodyPartDefinition>
            {
                new BodyPartDefinition
                {
                    Code = "head",
                    Name = "Head",
                    MaxHealth = 100,
                    VitalityImpact = 0.8f,
                    BleedingRate = 2.0f,
                    CanBreak = false
                },
                new BodyPartDefinition
                {
                    Code = "torso",
                    Name = "Torso",
                    MaxHealth = 150,
                    VitalityImpact = 1.0f,
                    BleedingRate = 1.5f,
                    CanBreak = false
                },
                new BodyPartDefinition
                {
                    Code = "leftarm",
                    Name = "Left Arm",
                    MaxHealth = 75,
                    VitalityImpact = 0.3f,
                    BleedingRate = 1.0f,
                    CanBreak = true
                },
                new BodyPartDefinition
                {
                    Code = "rightarm",
                    Name = "Right Arm",
                    MaxHealth = 75,
                    VitalityImpact = 0.3f,
                    BleedingRate = 1.0f,
                    CanBreak = true
                },
                new BodyPartDefinition
                {
                    Code = "leftleg",
                    Name = "Left Leg",
                    MaxHealth = 75,
                    VitalityImpact = 0.4f,
                    BleedingRate = 1.0f,
                    CanBreak = true
                },
                new BodyPartDefinition
                {
                    Code = "rightleg",
                    Name = "Right Leg",
                    MaxHealth = 75,
                    VitalityImpact = 0.4f,
                    BleedingRate = 1.0f,
                    CanBreak = true
                }
            };
            
            foreach (var bodyPartDef in bodyParts)
            {
                bodyPartRegistry[bodyPartDef.Code] = bodyPartDef;
            }
        }
        
        private void LoadProcedureDefinitions()
        {
            // For now, hardcode some procedure definitions
            // Eventually these would be loaded from a JSON config file
            List<SurgicalProcedureDefinition> procedures = new List<SurgicalProcedureDefinition>
            {
                new SurgicalProcedureDefinition
                {
                    Code = "treat_bleeding",
                    Name = "Treat Bleeding",
                    Description = "Stop bleeding from a wound",
                    RequiredTools = new List<string> { "scalpel", "forceps", "suture", "bandage" },
                    Steps = new List<SurgicalProcedureStep>
                    {
                        new SurgicalProcedureStep
                        {
                            Description = "Clean the wound",
                            RequiredTool = "scalpel",
                            SuccessRate = 0.9f,
                            TimeRequired = 5
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Extract foreign objects",
                            RequiredTool = "forceps",
                            SuccessRate = 0.8f,
                            TimeRequired = 10
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Close the wound",
                            RequiredTool = "suture",
                            SuccessRate = 0.7f,
                            TimeRequired = 15
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Apply bandage",
                            RequiredTool = "bandage",
                            SuccessRate = 0.95f,
                            TimeRequired = 5
                        }
                    },
                    ApplicableConditions = new List<MedicalConditionType> { MedicalConditionType.Bleeding },
                    TargetOutcome = MedicalConditionType.None
                },
                new SurgicalProcedureDefinition
                {
                    Code = "treat_infection",
                    Name = "Treat Infection",
                    Description = "Treat an infected wound",
                    RequiredTools = new List<string> { "scalpel", "forceps", "disinfectant", "bandage" },
                    Steps = new List<SurgicalProcedureStep>
                    {
                        new SurgicalProcedureStep
                        {
                            Description = "Open the wound",
                            RequiredTool = "scalpel",
                            SuccessRate = 0.9f,
                            TimeRequired = 5
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Clean infected tissue",
                            RequiredTool = "forceps",
                            SuccessRate = 0.7f,
                            TimeRequired = 15
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Apply disinfectant",
                            RequiredTool = "disinfectant",
                            SuccessRate = 0.8f,
                            TimeRequired = 10
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Dress the wound",
                            RequiredTool = "bandage",
                            SuccessRate = 0.95f,
                            TimeRequired = 5
                        }
                    },
                    ApplicableConditions = new List<MedicalConditionType> { MedicalConditionType.Infection },
                    TargetOutcome = MedicalConditionType.None
                },
                new SurgicalProcedureDefinition
                {
                    Code = "set_broken_bone",
                    Name = "Set Broken Bone",
                    Description = "Set and stabilize a broken bone",
                    RequiredTools = new List<string> { "forceps", "bone_saw", "splint", "bandage" },
                    Steps = new List<SurgicalProcedureStep>
                    {
                        new SurgicalProcedureStep
                        {
                            Description = "Align bone fragments",
                            RequiredTool = "forceps",
                            SuccessRate = 0.7f,
                            TimeRequired = 15
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Remove bone fragments if necessary",
                            RequiredTool = "bone_saw",
                            SuccessRate = 0.6f,
                            TimeRequired = 20
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Apply splint",
                            RequiredTool = "splint",
                            SuccessRate = 0.8f,
                            TimeRequired = 10
                        },
                        new SurgicalProcedureStep
                        {
                            Description = "Secure with bandage",
                            RequiredTool = "bandage",
                            SuccessRate = 0.9f,
                            TimeRequired = 5
                        }
                    },
                    ApplicableConditions = new List<MedicalConditionType> { MedicalConditionType.BrokenBone },
                    TargetOutcome = MedicalConditionType.None
                }
            };
            
            foreach (var procedureDef in procedures)
            {
                procedureRegistry[procedureDef.Code] = procedureDef;
            }
        }
        
        private void MapEntityBodyParts()
        {
            // Map entity types to body parts
            // For players and humanoid entities
            entityBodyParts["player"] = new List<string> { "head", "torso", "leftarm", "rightarm", "leftleg", "rightleg" };
            
            // Other entity mappings could be added here
            entityBodyParts["humanoid"] = new List<string> { "head", "torso", "leftarm", "rightarm", "leftleg", "rightleg" };
            entityBodyParts["quadruped"] = new List<string> { "head", "torso", "frontleftleg", "frontrightleg", "backleftleg", "backrightleg" };
        }
        
        public List<string> GetEntityBodyParts(Entity entity)
        {
            // Get appropriate body parts based on entity type
            string entityType = "humanoid"; // Default to humanoid
            
            if (entity is EntityPlayer)
            {
                entityType = "player";
            }
            else if (entity.Properties.Attributes?["bodyType"].Exists == true)
            {
                entityType = entity.Properties.Attributes["bodyType"].AsString();
            }
            
            if (entityBodyParts.TryGetValue(entityType, out List<string> parts))
            {
                return parts;
            }
            
            // Fall back to humanoid if specific mapping isn't found
            return entityBodyParts["humanoid"];
        }
        
        public List<BodyPartCondition> GetEntityConditions(Entity entity)
        {
            var conditions = new List<BodyPartCondition>();
            
            // Get the medical condition behavior
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                // If the entity doesn't have a medical condition behavior, return empty list
                return conditions;
            }
            
            // Get entity body parts and their conditions
            return medicalBehavior.BodyPartConditions;
        }
        
        public List<SurgicalProcedureDefinition> GetAvailableProcedures(Entity entity, string bodyPartCode)
        {
            var availableProcedures = new List<SurgicalProcedureDefinition>();
            
            // Get the medical condition behavior
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                // If the entity doesn't have a medical condition behavior, no procedures are available
                return availableProcedures;
            }
            
            // Find the body part condition
            var bodyPartCondition = medicalBehavior.BodyPartConditions.FirstOrDefault(bp => bp.BodyPartCode == bodyPartCode);
            if (bodyPartCondition == null)
            {
                // If the body part doesn't exist or has no conditions, no procedures are available
                return availableProcedures;
            }
            
            // Filter procedures applicable to this body part's conditions
            foreach (var procedureDef in procedureRegistry.Values)
            {
                if (procedureDef.ApplicableConditions.Contains(bodyPartCondition.ConditionType))
                {
                    availableProcedures.Add(procedureDef);
                }
            }
            
            return availableProcedures;
        }
        
        public bool StartSurgery(IServerPlayer player, Entity targetEntity, string bodyPartCode, string procedureCode)
        {
            if (player == null || targetEntity == null)
            {
                return false;
            }
            
            // Check if the target entity has a medical condition behavior
            var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "This entity cannot be operated on.", EnumChatType.CommandError);
                return false;
            }
            
            // Check if the body part exists and has a condition that can be treated
            var bodyPartCondition = medicalBehavior.BodyPartConditions.FirstOrDefault(bp => bp.BodyPartCode == bodyPartCode);
            if (bodyPartCondition == null || bodyPartCondition.ConditionType == MedicalConditionType.None)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "This body part doesn't need treatment.", EnumChatType.CommandError);
                return false;
            }
            
            // Check if the procedure exists and is applicable
            if (!procedureRegistry.TryGetValue(procedureCode, out SurgicalProcedureDefinition procedure))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Unknown surgical procedure.", EnumChatType.CommandError);
                return false;
            }
            
            if (!procedure.ApplicableConditions.Contains(bodyPartCondition.ConditionType))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "This procedure cannot treat the condition.", EnumChatType.CommandError);
                return false;
            }
            
            // Create surgery state
            var surgeryState = new SurgeryProcedureState
            {
                TargetEntityId = targetEntity.EntityId,
                BodyPartCode = bodyPartCode,
                ProcedureCode = procedureCode,
                CurrentStepIndex = 0,
                LastStepTime = api.World.Calendar.TotalHours
            };
            
            // Store the surgery state
            string playerUid = player.PlayerUID;
            if (!activeSurgeries.TryGetValue(playerUid, out Dictionary<string, SurgeryProcedureState> playerSurgeries))
            {
                playerSurgeries = new Dictionary<string, SurgeryProcedureState>();
                activeSurgeries[playerUid] = playerSurgeries;
            }
            
            string surgeryKey = $"{targetEntity.EntityId}:{bodyPartCode}";
            playerSurgeries[surgeryKey] = surgeryState;
            
            // Notify the player
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"Starting procedure: {procedure.Name} on {bodyPartRegistry[bodyPartCode].Name}", EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"First step: {procedure.Steps[0].Description} using {procedure.Steps[0].RequiredTool}", EnumChatType.CommandSuccess);
            
            // Notify nearby players
            NotifyNearbyPlayers(player, targetEntity, $"{player.PlayerName} begins a surgical procedure on {targetEntity.GetName()}");
            
            return true;
        }
        
        public bool PerformSurgeryStep(IServerPlayer player, Entity targetEntity, string toolCode)
        {
            if (player == null || targetEntity == null)
            {
                return false;
            }
            
            // Get player's active surgeries
            string playerUid = player.PlayerUID;
            if (!activeSurgeries.TryGetValue(playerUid, out Dictionary<string, SurgeryProcedureState> playerSurgeries) || playerSurgeries.Count == 0)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You're not performing any surgeries.", EnumChatType.CommandError);
                return false;
            }
            
            // Find matching surgery for this entity
            string surgeryKey = playerSurgeries.Keys.FirstOrDefault(key => key.StartsWith(targetEntity.EntityId.ToString()));
            if (surgeryKey == null || !playerSurgeries.TryGetValue(surgeryKey, out SurgeryProcedureState surgeryState))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You're not performing surgery on this entity.", EnumChatType.CommandError);
                return false;
            }
            
            // Get the procedure
            if (!procedureRegistry.TryGetValue(surgeryState.ProcedureCode, out SurgicalProcedureDefinition procedure))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Procedure not found. Surgery cancelled.", EnumChatType.CommandError);
                playerSurgeries.Remove(surgeryKey);
                return false;
            }
            
            // Check if using the correct tool for the current step
            SurgicalProcedureStep currentStep = procedure.Steps[surgeryState.CurrentStepIndex];
            if (currentStep.RequiredTool != toolCode)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Wrong tool. This step requires a {currentStep.RequiredTool}.", EnumChatType.CommandError);
                return false;
            }
            
            // Check if enough time has passed since the last step (if not the first step)
            double currentTime = api.World.Calendar.TotalHours;
            double timeSinceLastStep = (currentTime - surgeryState.LastStepTime) * 60; // Convert to minutes
            if (surgeryState.CurrentStepIndex > 0 && timeSinceLastStep < currentStep.TimeRequired)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"You need to wait {currentStep.TimeRequired - (int)timeSinceLastStep} more minutes before this step.", EnumChatType.CommandError);
                return false;
            }
            
            // Get tool quality from registry
            var toolRegistry = api.ModLoader.GetModSystem<SurgeryModSystem>().ToolRegistry;
            float qualityModifier = 1.0f; // Default if tool not found
            
            if (toolRegistry != null)
            {
                var toolDef = toolRegistry.GetToolDefinition(toolCode);
                if (toolDef != null)
                {
                    qualityModifier = toolDef.QualityModifier;
                }
            }
            
            // Perform the step with success chance modified by tool quality
            float adjustedSuccessRate = Math.Min(currentStep.SuccessRate * qualityModifier, 0.95f); // Cap at 95% success
            bool success = api.World.Rand.NextDouble() < adjustedSuccessRate;
            surgeryState.LastStepTime = currentTime;
            
            // Provide feedback about tool quality
            string qualityMessage = "";
            if (qualityModifier < 0.7f)
                qualityMessage = "The poor quality of your tool makes this difficult.";
            else if (qualityModifier < 0.9f)
                qualityMessage = "Your tool seems adequate, but not ideal for this task.";
            else if (qualityModifier < 1.1f)
                qualityMessage = "Your tool is well-suited for this procedure.";
            else
                qualityMessage = "The excellent quality of your tool makes this easier.";
                
            player.SendMessage(GlobalConstants.GeneralChatGroup, qualityMessage, EnumChatType.Notification);
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"Chance of success: {adjustedSuccessRate * 100:0}%", EnumChatType.Notification);
            
            if (!success)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You failed this surgical step. Try again.", EnumChatType.CommandError);
                
                // Check for patient damage on failure
                if (api.World.Rand.NextDouble() < 0.3) // 30% chance of causing damage on failure
                {
                    ModifyEntityHealth(targetEntity, -5); // Damage the patient
                    NotifyNearbyPlayers(player, targetEntity, $"{player.PlayerName} makes a mistake during surgery, causing {targetEntity.GetName()} pain!");
                }
                
                return true; // Still count as performed, but failed
            }
            
            // Step succeeded
            surgeryState.CurrentStepIndex++;
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"Surgical step completed successfully!", EnumChatType.CommandSuccess);
            
            // Check if procedure is complete
            if (surgeryState.CurrentStepIndex >= procedure.Steps.Count)
            {
                // Procedure complete, apply effects
                CompleteProcedure(player, targetEntity, surgeryState, procedure);
                playerSurgeries.Remove(surgeryKey);
                return true;
            }
            
            // Notify about next step
            SurgicalProcedureStep nextStep = procedure.Steps[surgeryState.CurrentStepIndex];
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"Next step: {nextStep.Description} using {nextStep.RequiredTool}", EnumChatType.CommandSuccess);
            
            return true;
        }
        
        private void CompleteProcedure(IServerPlayer player, Entity targetEntity, SurgeryProcedureState surgeryState, SurgicalProcedureDefinition procedure)
        {
            // Get the medical condition behavior
            var medicalBehavior = targetEntity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior == null)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Surgery completed, but the patient's condition has changed.", EnumChatType.CommandError);
                return;
            }
            
            // Find and update the body part condition
            var bodyPartCondition = medicalBehavior.BodyPartConditions.FirstOrDefault(bp => bp.BodyPartCode == surgeryState.BodyPartCode);
            if (bodyPartCondition != null)
            {
                bodyPartCondition.ConditionType = procedure.TargetOutcome;
                medicalBehavior.OnBodyPartConditionChanged(bodyPartCondition);
                
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Surgery completed successfully! The {bodyPartRegistry[surgeryState.BodyPartCode].Name} has been treated.", EnumChatType.CommandSuccess);
                NotifyNearbyPlayers(player, targetEntity, $"{player.PlayerName} has successfully completed surgery on {targetEntity.GetName()}");
                
                // Heal the entity a bit
                ModifyEntityHealth(targetEntity, 10);
            }
            else
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Surgery completed, but the body part was not found.", EnumChatType.CommandError);
            }
        }
        
        public void CancelSurgery(IServerPlayer player, Entity targetEntity)
        {
            if (player == null || targetEntity == null)
            {
                return;
            }
            
            // Get player's active surgeries
            string playerUid = player.PlayerUID;
            if (!activeSurgeries.TryGetValue(playerUid, out Dictionary<string, SurgeryProcedureState> playerSurgeries) || playerSurgeries.Count == 0)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You're not performing any surgeries.", EnumChatType.CommandError);
                return;
            }
            
            // Find matching surgery for this entity
            string surgeryKey = playerSurgeries.Keys.FirstOrDefault(key => key.StartsWith(targetEntity.EntityId.ToString()));
            if (surgeryKey == null)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "You're not performing surgery on this entity.", EnumChatType.CommandError);
                return;
            }
            
            // Remove the surgery
            playerSurgeries.Remove(surgeryKey);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Surgery cancelled.", EnumChatType.CommandSuccess);
            NotifyNearbyPlayers(player, targetEntity, $"{player.PlayerName} has cancelled the surgical procedure on {targetEntity.GetName()}");
        }
        
        private void NotifyNearbyPlayers(IPlayer player, Entity targetEntity, string message)
        {
            if (player == null || targetEntity == null || !(player is IServerPlayer serverPlayer))
            {
                return;
            }
            
            // Get players within 8 blocks
            var api = serverPlayer.Entity.Api as ICoreServerAPI;
            if (api == null) return;
            
            var nearbyPlayers = api.World.GetPlayersAround(targetEntity.Pos.XYZ, 8f, 8f);
            foreach (var nearbyPlayer in nearbyPlayers)
            {
                if (nearbyPlayer is IServerPlayer serverNearbyPlayer)
                {
                    serverNearbyPlayer.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.OthersMessage);
                }
            }
        }
        
        // Helper method to change entity health safely
        private void ModifyEntityHealth(Entity entity, float amount)
        {
            if (entity == null) return;
            
            // Try to get the entity's health behavior
            var healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBehavior != null)
            {
                // Modify health through the health behavior
                healthBehavior.Health += amount;
                
                // Trigger damage or healing animations
                if (amount < 0 && entity.World.Side == EnumAppSide.Server)
                {
                    entity.World.PlaySoundAt(new AssetLocation("sounds/damage"), entity);
                }
                
                // Kill entity if health reaches 0
                if (healthBehavior.Health <= 0 && entity.Alive)
                {
                    entity.Die(EnumDespawnReason.Death);
                }
            }
            else
            {
                // If no health behavior, try using attributes directly
                if (entity.WatchedAttributes.HasAttribute("health"))
                {
                    float health = entity.WatchedAttributes.GetFloat("health");
                    float maxHealth = entity.WatchedAttributes.GetFloat("maxhealth", 20f);
                    
                    health = Math.Min(Math.Max(0, health + amount), maxHealth);
                    entity.WatchedAttributes.SetFloat("health", health);
                    
                    // Kill entity if health reaches 0
                    if (health <= 0 && entity.Alive)
                    {
                        entity.Die(EnumDespawnReason.Death);
                    }
                }
            }
        }
    }
} 