using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace thebasics.ModSystems.Surgery.Models
{
    #region Configuration Models
    
    public class SurgeryConfig
    {
        public List<BodyPartDefinition> BodyParts { get; set; } = new List<BodyPartDefinition>();
        public List<SurgicalProcedure> Procedures { get; set; } = new List<SurgicalProcedure>();
        public List<SurgicalToolDefinition> Tools { get; set; } = new List<SurgicalToolDefinition>();
        
        // Surgery settings
        public float SurgeryFailChancePerStep { get; set; } = 0.1f;
        public float SurgeryFailChanceModifierPerSkillLevel { get; set; } = -0.01f;
        public bool RequireOperatingTable { get; set; } = true;
        public bool RequireSterilization { get; set; } = true;
        public float BleedingChanceOnFail { get; set; } = 0.3f;
        public float InfectionChanceOnFail { get; set; } = 0.2f;
    }
    
    public class BodyPartDefinition
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public float MaxHealth { get; set; } = 100f;
        public float VitalityImpact { get; set; } = 1.0f;
        public float BleedingRate { get; set; } = 1.0f;
        public bool CanBreak { get; set; } = false;
    }
    
    public class SurgicalProcedureDefinition
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> RequiredTools { get; set; } = new List<string>();
        public List<SurgicalProcedureStep> Steps { get; set; } = new List<SurgicalProcedureStep>();
        public List<MedicalConditionType> ApplicableConditions { get; set; } = new List<MedicalConditionType>();
        public MedicalConditionType TargetOutcome { get; set; } = MedicalConditionType.None;
    }
    
    public class SurgicalProcedureStep
    {
        public string Description { get; set; }
        public string RequiredTool { get; set; }
        public float SuccessRate { get; set; } = 0.8f;
        public float TimeRequired { get; set; } = 5f; // In minutes
    }
    
    public class SurgicalToolDefinition
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool Consumable { get; set; } = false;
        public int UsesBeforeDegrading { get; set; } = 10;
    }
    
    public enum MedicalConditionType
    {
        None,
        Bleeding,
        Infection,
        BrokenBone,
        InternalDamage,
        Poisoning
    }
    
    #endregion
    
    #region Runtime Models
    
    public class EntityMedicalData
    {
        public long EntityId { get; set; }
        public Dictionary<string, BodyPartCondition> BodyPartConditions { get; set; } = new Dictionary<string, BodyPartCondition>();
        public SurgerySession ActiveSurgery { get; set; }
        
        // Medical conditions
        public bool IsInfected { get; set; }
        public bool IsBleeding { get; set; }
        public float InfectionLevel { get; set; }
        public float BleedingRate { get; set; }
        
        [JsonIgnore]
        public bool HasActiveSurgery => ActiveSurgery != null;
        
        public EntityMedicalData(long entityId)
        {
            EntityId = entityId;
        }
        
        public BodyPartCondition GetOrCreateBodyPartCondition(string bodyPartCode)
        {
            if (!BodyPartConditions.TryGetValue(bodyPartCode, out var condition))
            {
                condition = new BodyPartCondition { BodyPartCode = bodyPartCode };
                BodyPartConditions[bodyPartCode] = condition;
            }
            
            return condition;
        }
    }
    
    public class BodyPartCondition
    {
        public string BodyPartCode { get; set; }
        public float DamageLevel { get; set; }
        public bool IsBroken { get; set; }
        public bool IsBleeding { get; set; }
        public bool IsInfected { get; set; }
        public MedicalConditionType ConditionType { get; set; } = MedicalConditionType.None;
        
        public bool IsHealthy => DamageLevel <= 0 && !IsBroken && !IsBleeding && !IsInfected && ConditionType == MedicalConditionType.None;
    }
    
    public class SurgerySession
    {
        public string SurgeonId { get; set; }
        public long PatientEntityId { get; set; }
        public string BodyPartCode { get; set; }
        public string ProcedureCode { get; set; }
        public int CurrentStepIndex { get; set; }
        public double StartTime { get; set; }
        
        // Progress
        public List<SurgeryStepResult> CompletedSteps { get; set; } = new List<SurgeryStepResult>();
        public bool Failed { get; set; }
        public string FailureReason { get; set; }
    }
    
    public class SurgeryStepResult
    {
        public int StepIndex { get; set; }
        public string ToolUsed { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public double CompletionTime { get; set; }
    }
    
    public class SurgeryProcedureState
    {
        public long TargetEntityId { get; set; }
        public string BodyPartCode { get; set; }
        public string ProcedureCode { get; set; }
        public int CurrentStepIndex { get; set; }
        public double LastStepTime { get; set; }
    }
    
    #endregion
} 