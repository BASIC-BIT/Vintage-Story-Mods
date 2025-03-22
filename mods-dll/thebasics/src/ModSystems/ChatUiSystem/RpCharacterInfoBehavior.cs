using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem
{
    public class RpCharacterInfoBehavior : EntityBehavior
    {
        private ICoreClientAPI capi;
        private LoadedTexture textTexture;
        private string lastText = "";

        public RpCharacterInfoBehavior(Entity entity) : base(entity)
        {
            capi = (entity.Api as ICoreClientAPI);
        }

        public override string PropertyName()
        {
            return "rpcharacterinfo";
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (capi == null) return;

            string text = entity.WatchedAttributes.GetString("rpCharacterInfo", null);
            if (text == null) return;

            if (text != lastText)
            {
                lastText = text;
                if (textTexture != null && !textTexture.Disposed)
                {
                    textTexture.Dispose();
                }

                var font = CairoFont.WhiteDetailText();
                var textExtents = font.GetTextExtents(text);
                textTexture = new LoadedTexture(capi, (int)textExtents.Width, (int)(textExtents.Height * 3), false);

                capi.Render.LoadOrUpdateTextTexture(text, font, out int texId);
                textTexture.TextureId = texId;
            }
        }

        public override void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || textTexture == null || textTexture.Disposed) return;

            Vec3d aboveHeadPos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 + 0.5, 0);

            capi.Render.GlToggleBlend(true);
            capi.Render.GlDisableCullFace();
            capi.Render.BindTexture2d(textTexture.TextureId);
            capi.Render.RenderMesh(entity.Properties.Client.Renderer.MeshRef);
            capi.Render.GlEnableCullFace();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (textTexture != null && !textTexture.Disposed)
            {
                textTexture.Dispose();
                textTexture = null;
            }
        }
    }
} 