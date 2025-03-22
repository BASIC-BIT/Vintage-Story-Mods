using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using thebasics.Extensions;
using thebasics.Models;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace thebasics.ModSystems.ChatUiSystem
{
    [HarmonyPatch(typeof(EntityPlayerShapeRenderer))]
    public class RpTextEntityPlayerShapeRendererPatch
    {
        private static ICoreClientAPI capi;
        private static float accumulator;
        private static bool shouldRender;

        public static void Initialize(ICoreClientAPI api)
        {
            capi = api;
            accumulator = 0f;
            shouldRender = false;
        }

        [HarmonyPatch("RenderFrame")]
        [HarmonyPostfix]
        public static void OnRenderFrame3DPostfix(EntityPlayerShapeRenderer __instance, float dt)
        {
            if (capi == null) return;

            accumulator += dt;
            if (accumulator < 0.1f) return;
            accumulator = 0f;

            shouldRender = capi.Input.KeyboardKeyState[(int)GlKeys.ShiftLeft] || capi.Input.KeyboardKeyState[(int)GlKeys.ShiftRight];
            
            if (!shouldRender) return;

            if (__instance.entity is EntityPlayer player)
            {
                var sheet = (player.Player as IServerPlayer)?.GetCharacterSheet();
                if (sheet != null)
                {
                    string characterInfo = $"Height: {sheet.HeightCm}cm | Weight: {sheet.WeightKg}kg";
                    characterInfo += $"\nDemeanor: {sheet.Demeanor}";
                    characterInfo += $"\nAppearance: {sheet.PhysicalAppearance}";

                    double dx = __instance.entity.Pos.X - capi.World.Player.Entity.Pos.X;
                    double dy = __instance.entity.Pos.Y - capi.World.Player.Entity.Pos.Y;
                    double dz = __instance.entity.Pos.Z - capi.World.Player.Entity.Pos.Z;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    float scale = 0.02f;
                    if (dist > 3) scale *= 3 / dist;

                    Vec3d aboveHeadPos = __instance.entity.Pos.XYZ.Add(0, __instance.entity.SelectionBox.Y2 + 0.5, 0);

                    // Draw text using the entity's nameplate system
                    __instance.entity.WatchedAttributes.SetString("rpCharacterInfo", characterInfo);
                    __instance.entity.WatchedAttributes.MarkPathDirty("rpCharacterInfo");
                }
            }
        }
    }
} 