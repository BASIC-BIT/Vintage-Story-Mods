using System;
using System.Collections.Generic;
using System.Text;
using thebasics.ModSystems.Surgery.UI;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.Surgery
{
    public class BedSurgeryHandler
    {
        private ICoreServerAPI api;
        private SurgerySystem surgerySystem;

        // Mapping of hit box segments to body parts
        private Dictionary<string, string> hitBoxToBodyPart = new Dictionary<string, string>
        {
            { "head", "head" },
            { "body", "torso" },
            { "leftarm", "leftarm" },
            { "rightarm", "rightarm" },
            { "leftleg", "leftleg" },
            { "rightleg", "rightleg" }
        };

        public BedSurgeryHandler(ICoreServerAPI api, SurgerySystem surgerySystem)
        {
            this.api = api;
            this.surgerySystem = surgerySystem;

            // Register the entity interaction event
            api.Event.OnPlayerInteractEntity += OnPlayerInteractEntity;
        }

        private void OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
        {
            // Check if the entity is a player
            if (!(entity is EntityPlayer targetEntityPlayer) || !(byPlayer is IServerPlayer serverPlayer))
                return;

            // Check if the player is on a bed
            if (!IsPlayerOnBed(targetEntityPlayer))
                return;

            // Check if the player is holding a surgical tool
            var surgeryModSystem = api.ModLoader.GetModSystem<SurgeryModSystem>();
            string toolCode = null;
            if (slot?.Itemstack != null && surgeryModSystem?.ToolRegistry != null)
            {
                toolCode = surgeryModSystem.ToolRegistry.GetToolCode(slot.Itemstack);
            }

            // If not holding a surgical tool, ignore the interaction
            if (toolCode == null)
                return;

            // Determine which body part was clicked
            var bodyPartCode = GetTargetedBodyPart(entity, hitPosition);
            if (bodyPartCode == null)
            {
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Could not determine which body part you're targeting.", EnumChatType.CommandError);
                return;
            }

            // Get available procedures for this body part
            var availableProcedures = surgerySystem.GetAvailableProcedures(entity, bodyPartCode);
            if (availableProcedures.Count == 0)
            {
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "No surgical procedures available for this body part.", EnumChatType.CommandError);
                return;
            }

            // Present dialog to select a procedure
            ShowProcedureSelectionDialog(serverPlayer, targetEntityPlayer.Player, entity, bodyPartCode, availableProcedures);

            // Mark the interaction as handled
            handling = EnumHandling.PreventDefault;
        }

        private bool IsPlayerOnBed(EntityPlayer player)
        {
            // In Vintage Story, we can check if a player is on a bed by checking their animation state
            // or by looking at the block they're standing on
            
            // Method 1: Check animation state (if applicable)
            if (player.AnimManager.ActiveAnimationsByAnimCode.ContainsKey("sleep") || 
                player.AnimManager.ActiveAnimationsByAnimCode.ContainsKey("lying"))
                return true;
            
            // Method 2: Check if player is on a bed block
            BlockPos pos = player.Pos.AsBlockPos;
            Block block = api.World.BlockAccessor.GetBlock(pos);
            
            // Check if the player is on a bed block (matching beds by name since there's no direct interface)
            if (block.Code.Path.Contains("bed") || block.FirstCodePart().Contains("bed"))
                return true;
            
            // Check one block below if the player is slightly elevated
            pos.Down();
            block = api.World.BlockAccessor.GetBlock(pos);
            if (block.Code.Path.Contains("bed") || block.FirstCodePart().Contains("bed"))
                return true;
            
            return false;
        }

        private string GetTargetedBodyPart(Entity entity, Vec3d hitPosition)
        {
            // Convert hit position to entity-local coordinates
            Vec3d localHit = hitPosition.Clone().Subtract(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            
            // Adjust for entity height to get relative position
            float entityHeight = entity.SelectionBox.Y2 - entity.SelectionBox.Y1;
            float relativeY = (float)(localHit.Y / entityHeight);
            
            // Simple height-based detection
            if (relativeY > 0.8f) // Top 20% of entity
                return "head";
            else if (relativeY > 0.5f) // Upper body
                return "torso";
            else if (relativeY > 0.2f) // Mid body
            {
                // Determine left or right based on X position
                if (localHit.X > 0)
                    return "rightarm";
                else
                    return "leftarm";
            }
            else // Lower body
            {
                // Determine left or right based on X position
                if (localHit.X > 0)
                    return "rightleg";
                else
                    return "leftleg";
            }
        }

        private void ShowProcedureSelectionDialog(IServerPlayer player, IServerPlayer targetPlayer, Entity targetEntity, string bodyPartCode, List<SurgicalProcedureDefinition> procedures)
        {
            // Create a dialog with buttons for each procedure
            var dialogTitle = $"Select Surgery for {targetPlayer.PlayerName}'s {surgerySystem.GetBodyPartName(bodyPartCode)}";
            var sb = new StringBuilder();
            
            sb.AppendLine($"Patient: {targetPlayer.PlayerName}");
            sb.AppendLine($"Body Part: {surgerySystem.GetBodyPartName(bodyPartCode)}");
            sb.AppendLine();
            sb.AppendLine("Available Procedures:");
            
            // Create the dialog
            var dialog = new DialogBuilder(dialogTitle, sb.ToString())
                .SetThingToTrack(targetEntity)
                .SetManualClose(true);
            
            // Add buttons for each procedure
            foreach (var procedure in procedures)
            {
                dialog.AddButton(procedure.Name, () => {
                    surgerySystem.StartSurgery(player, targetEntity, bodyPartCode, procedure.Code);
                });
            }
            
            // Add cancel button
            dialog.AddButton("Cancel", () => { });
            
            // Send dialog to player
            dialog.SendTo(player);
        }
    }
} 