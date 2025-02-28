using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using thebasics.ModSystems.Surgery.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Surgery.Behaviors
{
    public class MedicalConditionBehavior : EntityBehavior
    {
        private EntityMedicalData medicalData;
        private SurgerySystem surgerySystem;
        private ICoreServerAPI api;
        private long nextTickTime;
        private static readonly Random random = new Random();
        
        // Medical condition settings
        private const float BleedingDamagePerTick = 0.5f;
        private const float InfectionDamagePerTick = 0.25f;
        private const int TickIntervalMs = 5000; // 5 seconds
        
        // Add a property to expose body part conditions
        public List<BodyPartCondition> BodyPartConditions => new List<BodyPartCondition>(medicalData.BodyPartConditions.Values);
        
        public MedicalConditionBehavior(Entity entity) : base(entity)
        {
            medicalData = new EntityMedicalData(entity.EntityId);
        }
        
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            
            if (entity.Api is ICoreServerAPI serverApi)
            {
                api = serverApi;
                surgerySystem = SurgerySystem.Instance;
                
                // Load medical data if available
                if (entity.WatchedAttributes.HasAttribute("medicalData"))
                {
                    try
                    {
                        string json = entity.WatchedAttributes.GetString("medicalData");
                        medicalData = JsonConvert.DeserializeObject<EntityMedicalData>(json) ?? new EntityMedicalData(entity.EntityId);
                    }
                    catch (Exception ex)
                    {
                        api.Server.LogError($"The BASICs: Failed to load medical data for entity {entity.EntityId}. Error: {ex.Message}");
                        medicalData = new EntityMedicalData(entity.EntityId);
                    }
                }
                
                // Set initial tick time
                nextTickTime = entity.World.ElapsedMilliseconds + TickIntervalMs;
                
                // Register to server events
                api.Event.RegisterGameTickListener(OnGameTick, 1000, 0);
            }
        }
        
        private void OnGameTick(float deltaTime)
        {
            if (entity.World.ElapsedMilliseconds < nextTickTime)
            {
                return;
            }
            
            nextTickTime = entity.World.ElapsedMilliseconds + TickIntervalMs;
            
            // Process medical conditions
            ProcessBleedingDamage();
            ProcessInfectionDamage();
            
            // Save medical data
            SaveMedicalData();
        }
        
        private void ProcessBleedingDamage()
        {
            if (!medicalData.IsBleeding)
            {
                return;
            }
            
            var healthComponent = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthComponent == null)
            {
                return;
            }
            
            // Apply bleeding damage
            float damage = BleedingDamagePerTick * medicalData.BleedingRate;
            healthComponent.Health -= damage;
            
            // Create blood particles
            if (api != null && random.NextDouble() < 0.5)
            {
                Vec3d entityPos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y1 / 2, 0);
                api.World.SpawnParticles(new SimpleParticleProperties
                {
                    MinPos = entityPos,
                    AddPos = new Vec3d(0.5, 0.5, 0.5),
                    MinQuantity = 1,
                    MaxQuantity = 5,
                    Color = ColorUtil.ColorFromRgba(155, 10, 10, 255),
                    LifeLength = 1.5f,
                    MinSize = 0.1f,
                    MaxSize = 0.2f,
                    MinVelocity = new Vec3f(-0.1f, -0.1f, -0.1f),
                    MaxVelocity = new Vec3f(0.1f, -0.05f, 0.1f),
                    GravityEffect = 1f
                });
            }
            
            // Check if bleeding should stop naturally
            if (random.NextDouble() < 0.05) // 5% chance per tick
            {
                medicalData.BleedingRate *= 0.8f; // Reduce bleeding rate
                
                if (medicalData.BleedingRate < 0.1f)
                {
                    medicalData.IsBleeding = false;
                    medicalData.BleedingRate = 0;
                    
                    // Notify nearby players
                    NotifyNearbyPlayers($"{entity.GetName()}'s bleeding has stopped naturally.");
                }
            }
        }
        
        private void ProcessInfectionDamage()
        {
            if (!medicalData.IsInfected)
            {
                return;
            }
            
            var healthComponent = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthComponent == null)
            {
                return;
            }
            
            // Apply infection damage
            float damage = InfectionDamagePerTick * medicalData.InfectionLevel;
            healthComponent.Health -= damage;
            
            // Infection progresses over time
            medicalData.InfectionLevel += 0.01f * (float)random.NextDouble();
            medicalData.InfectionLevel = Math.Min(medicalData.InfectionLevel, 2.0f);
            
            // Notify if infection gets worse
            if (medicalData.InfectionLevel > 1.5f && random.NextDouble() < 0.1)
            {
                NotifyNearbyPlayers($"{entity.GetName()} looks severely infected and needs medical attention!");
            }
            
            // Check if infection should clear naturally (very rare)
            if (random.NextDouble() < 0.01) // 1% chance per tick
            {
                medicalData.InfectionLevel *= 0.9f;
                
                if (medicalData.InfectionLevel < 0.1f)
                {
                    medicalData.IsInfected = false;
                    medicalData.InfectionLevel = 0;
                    
                    // Notify nearby players
                    NotifyNearbyPlayers($"{entity.GetName()}'s infection has cleared.");
                }
            }
        }
        
        private void NotifyNearbyPlayers(string message)
        {
            if (api == null)
            {
                return;
            }
            
            foreach (var player in api.World.AllPlayers)
            {
                if (player.Entity.Pos.SquareDistanceTo(entity.Pos) < 64) // 8 blocks radius
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
                }
            }
        }
        
        private void SaveMedicalData()
        {
            if (api == null)
            {
                return;
            }
            
            try
            {
                string json = JsonConvert.SerializeObject(medicalData);
                entity.WatchedAttributes.SetString("medicalData", json);
            }
            catch (Exception ex)
            {
                api.Server.LogError($"The BASICs: Failed to save medical data for entity {entity.EntityId}. Error: {ex.Message}");
            }
        }
        
        public EntityMedicalData GetMedicalData()
        {
            return medicalData;
        }
        
        public void SetBleeding(bool bleeding, float rate = 1.0f)
        {
            medicalData.IsBleeding = bleeding;
            medicalData.BleedingRate = rate;
            SaveMedicalData();
        }
        
        public void SetInfection(bool infected, float level = 1.0f)
        {
            medicalData.IsInfected = infected;
            medicalData.InfectionLevel = level;
            SaveMedicalData();
        }
        
        public void HealBodyPart(string bodyPartCode, float amount)
        {
            var bodyPart = medicalData.GetOrCreateBodyPartCondition(bodyPartCode);
            bodyPart.DamageLevel = Math.Max(0, bodyPart.DamageLevel - amount);
            
            // Clear conditions if fully healed
            if (bodyPart.DamageLevel <= 0)
            {
                bodyPart.IsBleeding = false;
                bodyPart.IsInfected = false;
            }
            
            // Update entity overall conditions
            UpdateEntityConditions();
            
            SaveMedicalData();
        }
        
        public void DamageBodyPart(string bodyPartCode, float amount, bool causeBleeding = false, bool causeInfection = false)
        {
            var bodyPart = medicalData.GetOrCreateBodyPartCondition(bodyPartCode);
            bodyPart.DamageLevel += amount;
            
            // Set conditions
            if (causeBleeding)
            {
                bodyPart.IsBleeding = true;
            }
            
            if (causeInfection)
            {
                bodyPart.IsInfected = true;
            }
            
            // Update entity overall conditions
            UpdateEntityConditions();
            
            SaveMedicalData();
        }
        
        private void UpdateEntityConditions()
        {
            bool anyBleeding = false;
            bool anyInfected = false;
            float bleedingRate = 0;
            float infectionLevel = 0;
            
            foreach (var condition in medicalData.BodyPartConditions.Values)
            {
                if (condition.IsBleeding)
                {
                    anyBleeding = true;
                    bleedingRate += 0.5f; // Each bleeding part contributes to overall rate
                }
                
                if (condition.IsInfected)
                {
                    anyInfected = true;
                    infectionLevel += 0.5f; // Each infected part contributes to overall level
                }
            }
            
            medicalData.IsBleeding = anyBleeding;
            medicalData.BleedingRate = bleedingRate;
            medicalData.IsInfected = anyInfected;
            medicalData.InfectionLevel = infectionLevel;
        }
        
        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            // Clean up events
            if (api != null)
            {
                api.Event.UnregisterGameTickListener(OnGameTick);
            }
            
            base.OnEntityDespawn(despawn);
        }
        
        public override string PropertyName()
        {
            return "medicalconditions";
        }
        
        // Add a method to handle body part condition changes
        public void OnBodyPartConditionChanged(BodyPartCondition condition)
        {
            if (condition == null) return;
            
            // Update the condition in the medical data
            medicalData.BodyPartConditions[condition.BodyPartCode] = condition;
            
            // Update overall entity conditions based on body parts
            UpdateEntityConditions();
            
            // Save the updated medical data
            SaveMedicalData();
        }
    }
} 