using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.HarmonyPatches;

[HarmonyPatch]
public static class EntityShapeRendererPatch
{
    [HarmonyTargetMethod]
    static MethodBase TargetMethod()
    {
        var type = typeof(EntityShapeRenderer);
        return AccessTools.Method(type, "OnChatMessage", new[] { typeof(int), typeof(string), typeof(EnumChatType), typeof(string) });
    }
    
    [HarmonyPrefix]
    static bool Prefix(object __instance, int groupId, string message, EnumChatType chattype, string data)
    {
        // Only process if this is RP text (marked with rptext: in data field)
        if (data == null || !data.Contains("rptext:")) 
            return true; // Let vanilla handle non-RP messages
        
        ICoreClientAPI capi = null;
        
        try
        {
            // Use reflection to access fields from the instance
            var instanceType = __instance.GetType();
            
            // Get capi from the instance (inherited from EntityRenderer)
            var capiField = AccessTools.Field(instanceType, "capi");
            if (capiField == null)
            {
                // Fallback - try to get it from base class
                capiField = AccessTools.Field(typeof(EntityRenderer), "capi");
            }
            
            if (capiField == null)
                return true; // Can't find capi field
            
            capi = capiField?.GetValue(__instance) as ICoreClientAPI;
            if (capi == null)
                return true; // No client API available
                
            // Add debug logging
            capi.Logger.Debug($"THEBASICS: EntityShapeRendererPatch processing message for group {groupId}, data: {data}");
            
            // Check if player is ready
            if (capi.World?.Player?.Entity == null)
            {
                capi.Logger.Debug("THEBASICS: Player entity not ready, skipping patch");
                return true; // Player not ready yet, let vanilla handle
            }
            
            // Get entity field
            var entityField = AccessTools.Field(instanceType, "entity");
            if (entityField == null)
            {
                // Try base class
                entityField = AccessTools.Field(typeof(EntityRenderer), "entity");
            }
            
            if (entityField == null)
            {
                capi.Logger.Warning("THEBASICS: Could not find entity field");
                return true;
            }
            
            var entity = entityField.GetValue(__instance) as Entity;
            if (entity == null)
            {
                capi.Logger.Debug("THEBASICS: Entity is null");
                return true;
            }
            
            // Check distance (same as vanilla)
            if (entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos.XYZ) >= 400.0)
            {
                capi.Logger.Debug($"THEBASICS: Entity too far away: {entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos.XYZ)}");
                return false; // Too far, don't show floating text
            }
            
            // Parse entity ID from data field
            // Format: "from: {entityId},rptext:"
            string[] parts = data.Split(',');
            if (parts.Length < 2) 
            {
                capi.Logger.Warning($"THEBASICS: Invalid data format: {data}");
                return true; // Invalid format, let vanilla handle
            }
            
            string[] idPart = parts[0].Split(':');
            if (idPart.Length < 2 || idPart[0].Trim() != "from")
            {
                capi.Logger.Warning($"THEBASICS: Invalid from format: {parts[0]}");
                return true; // Invalid format
            }
            
            if (!long.TryParse(idPart[1].Trim(), out long entityId))
            {
                capi.Logger.Warning($"THEBASICS: Could not parse entity ID: {idPart[1]}");
                return true; // Can't parse entity ID
            }
            
            // Check if this message is for this entity
            if (entity.EntityId != entityId)
            {
                capi.Logger.Debug($"THEBASICS: Message not for this entity. Expected: {entity.EntityId}, Got: {entityId}");
                return false; // Not for this entity, skip
            }
            
            // Get messageTextures field
            var messageTexturesField = AccessTools.Field(instanceType, "messageTextures");
            if (messageTexturesField == null)
            {
                capi.Logger.Warning("THEBASICS: Could not find messageTextures field");
                return true;
            }
            
            // Get or initialize the messageTextures list
            var messageTextures = messageTexturesField.GetValue(__instance) as IList;
            if (messageTextures == null)
            {
                capi.Logger.Debug("THEBASICS: messageTextures is null, attempting to create");
                
                // Try to find the MessageTexture type
                var messageTextureType = AccessTools.TypeByName("Vintagestory.GameContent.MessageTexture");
                if (messageTextureType == null)
                {
                    // Try alternate lookup with assembly name
                    messageTextureType = Type.GetType("Vintagestory.GameContent.MessageTexture, VSEssentials");
                }
                
                if (messageTextureType == null)
                {
                    capi.Logger.Error("THEBASICS: Could not find MessageTexture type");
                    return true;
                }
                
                // Create new list 
                var listType = typeof(List<>).MakeGenericType(messageTextureType);
                messageTextures = Activator.CreateInstance(listType) as IList;
                
                if (messageTextures == null)
                {
                    capi.Logger.Error("THEBASICS: Failed to create messageTextures list");
                    return true;
                }
                
                messageTexturesField.SetValue(__instance, messageTextures);
                capi.Logger.Debug("THEBASICS: Created new messageTextures list");
            }
            
            // Process the message - it's already translated/formatted
            var processedMessage = message.Replace("&lt;", "<").Replace("&gt;", ">");
            
            // Create texture using the already-translated message
            LoadedTexture tex = null;
            try
            {
                tex = capi.Gui.TextTexture.GenTextTexture(
                    processedMessage, 
                    new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble),
                    350,
                    new TextBackground
                    {
                        FillColor = GuiStyle.DialogLightBgColor,
                        Padding = 3,
                        Radius = GuiStyle.ElementBGRadius
                    },
                    EnumTextOrientation.Center
                );
            }
            catch (Exception texEx)
            {
                capi.Logger.Error($"THEBASICS: Failed to create texture: {texEx.Message}");
                return true;
            }
            
            if (tex == null)
            {
                capi.Logger.Error("THEBASICS: Generated texture is null");
                return true;
            }
            
            // Create MessageTexture instance via reflection
            var messageTextureType2 = AccessTools.TypeByName("Vintagestory.GameContent.MessageTexture");
            if (messageTextureType2 == null)
            {
                messageTextureType2 = Type.GetType("Vintagestory.GameContent.MessageTexture, VSEssentials");
            }
            
            if (messageTextureType2 == null)
            {
                capi.Logger.Error("THEBASICS: Could not find MessageTexture type for instantiation");
                return true;
            }
            
            var messageTextureInstance = Activator.CreateInstance(messageTextureType2);
            if (messageTextureInstance == null)
            {
                capi.Logger.Error("THEBASICS: Failed to create MessageTexture instance");
                return true;
            }
            
            // Set properties on the MessageTexture
            var texField = AccessTools.Field(messageTextureType2, "tex");
            var messageField = AccessTools.Field(messageTextureType2, "message");
            var receivedTimeField = AccessTools.Field(messageTextureType2, "receivedTime");
            
            if (texField == null || messageField == null || receivedTimeField == null)
            {
                capi.Logger.Error($"THEBASICS: Could not find MessageTexture fields. tex:{texField != null}, message:{messageField != null}, receivedTime:{receivedTimeField != null}");
                return true;
            }
            
            texField.SetValue(messageTextureInstance, tex);
            messageField.SetValue(messageTextureInstance, processedMessage);
            receivedTimeField.SetValue(messageTextureInstance, capi.World.ElapsedMilliseconds);
            
            // Insert at beginning of list (newest messages first)
            messageTextures.Insert(0, messageTextureInstance);
            
            capi.Logger.Debug($"THEBASICS: Successfully added floating text for entity {entityId}: {processedMessage}");
            
            // Skip vanilla processing since we handled it
            return false;
        }
        catch (Exception ex)
        {
            // Log error if we have capi available
            if (capi != null)
            {
                capi.Logger.Error($"THEBASICS: Exception in EntityShapeRendererPatch: {ex}");
            }
            return true; // Let vanilla handle on error
        }
    }
}