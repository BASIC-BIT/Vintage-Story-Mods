using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cairo;
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
            
            // Process the message - it's already translated/formatted with VTML
            var processedMessage = message.Replace("&lt;", "<").Replace("&gt;", ">");
            
            // Render VTML to texture with rich text support
            LoadedTexture tex = null;
            try
            {
                tex = RenderVtmlToTexture(capi, processedMessage);
            }
            catch (Exception texEx)
            {
                capi.Logger.Error($"THEBASICS: Failed to render VTML text: {texEx.Message}");
                // Fallback to plain text rendering
                try
                {
                    var plainText = System.Text.RegularExpressions.Regex.Replace(processedMessage, @"<[^>]+>", string.Empty);
                    tex = capi.Gui.TextTexture.GenTextTexture(
                        plainText, 
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
                catch
                {
                    return true;
                }
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
            
            capi.Logger.Debug($"THEBASICS: Successfully added rich text floating text for entity {entityId}");
            
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
    
    // Hardcoded flag to strip font tags for floating text
    // Font sizing is already handled by 3D perspective, so we don't need additional size changes
    private const bool StripFontTagsForFloatingText = true;
    
    private static LoadedTexture RenderVtmlToTexture(ICoreClientAPI capi, string vtmlText)
    {
        try
        {
            // Strip font tags if needed (font sizing is handled by 3D perspective)
            if (StripFontTagsForFloatingText)
            {
                vtmlText = System.Text.RegularExpressions.Regex.Replace(vtmlText, @"</?font[^>]*>", "");
            }
            
            // Parse VTML to rich text components
            var baseFont = new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble);
            var components = VtmlUtil.Richtextify(capi, vtmlText, baseFont, null);
            
            if (components == null || components.Length == 0)
            {
                return null;
            }
            
            // First pass: calculate required width
            double minWidth = 350;
            double maxWidth = 600;
            double requiredWidth = minWidth;
            
            // Do a preliminary calculation to find the natural width
            var testFlowPath = new TextFlowPath[] { new TextFlowPath(9999) }; // Very wide to get natural width
            foreach (var component in components)
            {
                if (component is RichTextComponent rtc)
                {
                    var textUtilField = AccessTools.Field(typeof(RichTextComponent), "textUtil");
                    if (textUtilField != null && textUtilField.GetValue(rtc) == null)
                    {
                        textUtilField.SetValue(rtc, new TextDrawUtil());
                    }
                }
                
                double nextX;
                component.CalcBounds(testFlowPath, 0, 0, 0, out nextX);
                
                if (component.BoundsPerLine != null && component.BoundsPerLine.Length > 0)
                {
                    foreach (var bounds in component.BoundsPerLine)
                    {
                        requiredWidth = Math.Max(requiredWidth, bounds.X + bounds.Width);
                    }
                }
            }
            
            // Clamp the width to reasonable bounds
            requiredWidth = Math.Min(maxWidth, Math.Max(minWidth, requiredWidth + 20)); // Add 20px padding
            
            // Create TextFlowPath with calculated width
            var flowPath = new TextFlowPath[] { new TextFlowPath(requiredWidth) };
            
            // Calculate bounds for all components with proper line height tracking
            double posX = 0;
            double posY = 0;
            double currentLineHeight = 0;
            double totalWidth = 0;
            double totalHeight = 0;
            
            foreach (var component in components)
            {
                // Initialize the component if it has an init method (for RichTextComponent)
                if (component is RichTextComponent rtc)
                {
                    // RichTextComponent constructor already calls init() if api is not null
                    // But we need to ensure TextDrawUtil is initialized
                    var textUtilField = AccessTools.Field(typeof(RichTextComponent), "textUtil");
                    if (textUtilField != null && textUtilField.GetValue(rtc) == null)
                    {
                        textUtilField.SetValue(rtc, new TextDrawUtil());
                    }
                }
                
                // Calculate bounds for this component
                double nextX;
                var result = component.CalcBounds(flowPath, currentLineHeight, posX, posY, out nextX);
                
                // Update dimensions based on component bounds
                if (component.BoundsPerLine != null && component.BoundsPerLine.Length > 0)
                {
                    // Track the maximum height for the current line
                    currentLineHeight = Math.Max(currentLineHeight, component.BoundsPerLine[0].Height);
                    
                    foreach (var bounds in component.BoundsPerLine)
                    {
                        totalWidth = Math.Max(totalWidth, bounds.X + bounds.Width);
                        totalHeight = Math.Max(totalHeight, bounds.Y + bounds.Height);
                    }
                    
                    // Handle multiline components
                    if (result == EnumCalcBoundsResult.Multiline && component.BoundsPerLine.Length > 1)
                    {
                        // Component spans multiple lines
                        var lastLine = component.BoundsPerLine[component.BoundsPerLine.Length - 1];
                        posY = lastLine.Y;
                        posX = lastLine.X + lastLine.Width;
                        // Set line height to the last line's height for next component
                        currentLineHeight = lastLine.Height;
                    }
                    else if (component.Float == EnumFloat.Inline)
                    {
                        // Continue on same line
                        posX = nextX;
                    }
                    else if (component.Float == EnumFloat.None)
                    {
                        // Start new line - move down by current line height
                        posX = 0;
                        posY += currentLineHeight;
                        currentLineHeight = 0; // Reset for new line
                    }
                }
            }
            
            // Ensure minimum dimensions
            totalWidth = Math.Max(10, totalWidth);
            totalHeight = Math.Max(currentLineHeight, Math.Max(10, totalHeight));
            
            // Create surface with padding
            int surfaceWidth = (int)Math.Ceiling(totalWidth + 6);
            int surfaceHeight = (int)Math.Ceiling(totalHeight + 6);
            
            ImageSurface surface = new ImageSurface(Format.Argb32, surfaceWidth, surfaceHeight);
            Context ctx = new Context(surface);
            
            // Draw background
            var bgColor = GuiStyle.DialogLightBgColor;
            ctx.SetSourceRGBA(bgColor[0], bgColor[1], bgColor[2], bgColor[3]);
            ctx.Rectangle(0, 0, surfaceWidth, surfaceHeight);
            ctx.Fill();
            
            // Apply padding and render components
            ctx.Save();
            ctx.Translate(3, 3);
            
            foreach (var component in components)
            {
                try
                {
                    component.ComposeElements(ctx, surface);
                }
                catch (Exception compEx)
                {
                    capi.Logger.Debug($"THEBASICS: Error rendering component: {compEx.Message}");
                }
            }
            
            ctx.Restore();
            
            // Convert surface to texture
            LoadedTexture texture = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);
            
            // Clean up
            ctx.Dispose();
            surface.Dispose();
            
            return texture;
        }
        catch (Exception ex)
        {
            capi.Logger.Warning($"THEBASICS: Failed to render rich text: {ex.Message}");
            
            // Fallback to simple text stripping
            try
            {
                var plainText = System.Text.RegularExpressions.Regex.Replace(vtmlText, @"<[^>]+>", string.Empty);
                if (string.IsNullOrWhiteSpace(plainText))
                {
                    return null;
                }
                
                return capi.Gui.TextTexture.GenTextTexture(
                    plainText,
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
            catch
            {
                return null;
            }
        }
    }
}