using System;
using System.Collections.Generic;
using thebasics.ModSystems.Surgery.Behaviors;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.Surgery.Handlers
{
    public class DamageHandler
    {
        private readonly ICoreServerAPI api;
        private readonly SurgerySystem surgerySystem;
        private readonly Random random = new Random();
        
        public DamageHandler(ICoreServerAPI api, SurgerySystem surgerySystem)
        {
            this.api = api;
            this.surgerySystem = surgerySystem;
            
            // Register to damage events
            api.Event.RegisterGameTickListener(CheckEntityDamage, 1000, 0);
            api.Event.EntityDeath += OnEntityDeath;
        }
        
        private void CheckEntityDamage(float dt)
        {
            // Process all entities with health behaviors
            foreach (var entity in api.World.LoadedEntities)
            {
                var healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
                var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
                
                if (healthBehavior != null && medicalBehavior != null)
                {
                    // Check if entity was recently damaged
                    float currentHealth = healthBehavior.Health;
                    
                    if (entity.WatchedAttributes.HasAttribute("lastHealth"))
                    {
                        float lastHealth = entity.WatchedAttributes.GetFloat("lastHealth");
                        
                        if (currentHealth < lastHealth)
                        {
                            // Entity took damage
                            float damageTaken = lastHealth - currentHealth;
                            ProcessEntityDamage(entity, damageTaken, medicalBehavior);
                        }
                    }
                    
                    // Update last health
                    entity.WatchedAttributes.SetFloat("lastHealth", currentHealth);
                }
            }
        }
        
        private void ProcessEntityDamage(Entity entity, float damageTaken, MedicalConditionBehavior medicalBehavior)
        {
            // Determine which body part took damage
            string bodyPartCode = DetermineAffectedBodyPart(entity);
            
            // Determine if damage should cause conditions
            bool causeBleeding = random.NextDouble() < 0.3; // 30% chance of bleeding
            bool causeInfection = random.NextDouble() < 0.1; // 10% chance of infection
            
            // Apply damage to body part
            medicalBehavior.DamageBodyPart(bodyPartCode, damageTaken, causeBleeding, causeInfection);
            
            // Notify nearby players
            if (causeBleeding || causeInfection)
            {
                string message = $"{entity.GetName()}'s {bodyPartCode} was injured";
                if (causeBleeding) message += " and is bleeding";
                if (causeInfection) message += " and may be infected";
                
                NotifyNearbyPlayers(entity, message);
            }
        }
        
        private string DetermineAffectedBodyPart(Entity entity)
        {
            // List of body parts with their chances
            var bodyPartChances = new Dictionary<string, float>
            {
                { "head", 0.1f },    // 10% chance to hit head
                { "chest", 0.3f },   // 30% chance to hit chest
                { "leftarm", 0.15f }, // 15% chance to hit left arm
                { "rightarm", 0.15f }, // 15% chance to hit right arm
                { "leftleg", 0.15f }, // 15% chance to hit left leg
                { "rightleg", 0.15f }  // 15% chance to hit right leg
            };
            
            // Get a random number
            double roll = random.NextDouble();
            double cumulativeChance = 0;
            
            // Determine which body part was hit
            foreach (var pair in bodyPartChances)
            {
                cumulativeChance += pair.Value;
                if (roll < cumulativeChance)
                {
                    return pair.Key;
                }
            }
            
            // Default to chest if something goes wrong
            return "chest";
        }
        
        private void NotifyNearbyPlayers(Entity entity, string message)
        {
            foreach (var player in api.World.AllPlayers)
            {
                if (player.Entity.Pos.SquareDistanceTo(entity.Pos) < 64) // 8 blocks radius
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
                }
            }
        }
        
        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            // Clean up medical behavior
            var medicalBehavior = entity.GetBehavior<MedicalConditionBehavior>();
            if (medicalBehavior != null)
            {
                // Could store dead body information for revival surgeries
                // or just clean up
            }
        }
        
        public void Dispose()
        {
            api.Event.UnregisterGameTickListener(CheckEntityDamage);
            api.Event.EntityDeath -= OnEntityDeath;
        }
    }
} 