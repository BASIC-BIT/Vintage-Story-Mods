using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A native character owned only by the setup panel, never registered in the world.</summary>
public sealed class SetupGuidePreview : IDisposable
{
    private const float FrameStep = 1f / 60f;
    private static long nextEntityId = -20000000;
    private readonly ICoreClientAPI api;
    private readonly GuideFixture fixture;
    private readonly DetachedGuideEntity actor;
    private GuideRenderer renderer;
    private ClientAnimator previewAnimator;
    private string animationCode = "idle";
    private double previewTime;
    private double frameRemainder;
    private bool namedTime;
    private bool disposed;

    public bool IsReady => !disposed && renderer?.IsReady == true && actor.AnimManager.Animator != null;

    public SetupGuidePreview(ICoreClientAPI api)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
        fixture = api.Assets.Get(new AssetLocation("thebasics:config/setup-guide.json")).ToObject<GuideFixture>();
        if (fixture?.SkinParts == null || fixture.Gear == null || fixture.Animations == null ||
            !fixture.Animations.Contains("idle", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The setup guide fixture is incomplete.");
        }

        actor = new DetachedGuideEntity { EntityId = Interlocked.Decrement(ref nextEntityId) };
        try
        {
            var source = api.World.GetEntityType(new AssetLocation("game:player"))
                ?? throw new InvalidOperationException("The native player entity is unavailable.");
            var properties = CopyProperties(source, api);
            var appliedParts = new TreeAttribute();
            foreach (var part in fixture.SkinParts)
            {
                appliedParts.SetString(part.Key, part.Value);
            }
            actor.WatchedAttributes["skinConfig"] = new TreeAttribute { ["appliedParts"] = appliedParts };
            actor.WatchedAttributes.SetString("voicetype", fixture.SkinParts["voicetype"]);
            actor.WatchedAttributes.SetString("voicepitch", fixture.SkinParts["voicepitch"]);
            actor.WatchedAttributes["nametag"] = new TreeAttribute { ["name"] = new StringAttribute(fixture.Name) };

            // A private native gear inventory, with no player UID or inventory-manager lookup.
            var gear = new InventoryGear("setupguide-" + actor.EntityId, api);
            foreach (var garment in fixture.Gear)
            {
                if (garment.Slot < 0 || garment.Slot >= gear.Count || !gear[garment.Slot].Empty)
                {
                    throw new InvalidOperationException("Invalid setup guide gear slot.");
                }
                var item = api.World.GetItem(new AssetLocation(garment.Code))
                    ?? throw new InvalidOperationException("Setup guide garment is unavailable: " + garment.Code);
                gear[garment.Slot].Itemstack = new ItemStack(item);
            }
            var inventoryTree = new TreeAttribute();
            gear.ToTreeAttributes(inventoryTree);
            actor.WatchedAttributes["seraphinventory"] = inventoryTree;

            // EntityPlayerBot finds this private behavior after Entity.Initialize fires OnInitialized.
            actor.OnInitialized += AddPrivateInventory;
            actor.Initialize(properties, api, 0);
            actor.OnInitialized -= AddPrivateInventory;
            ValidateAppearance();
            renderer = new GuideRenderer(actor, api);
            properties.Client.Renderer = renderer;
            actor.OnEntityLoaded();
            renderer.TesselateShape();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void AddPrivateInventory()
    {
        var inventory = new GuideInventory(actor);
        actor.AddBehavior(inventory);
        inventory.Initialize(actor.Properties, new JsonObject(Newtonsoft.Json.Linq.JObject.FromObject(new { code = "seraphinventory" })));
    }

    private void ValidateAppearance()
    {
        var skin = actor.GetBehavior<EntityBehaviorExtraSkinnable>();
        foreach (var part in fixture.SkinParts)
        {
            if (skin == null || !skin.AvailableSkinPartsByCode.TryGetValue(part.Key, out var available) ||
                !available.VariantsByCode.ContainsKey(part.Value))
            {
                throw new InvalidOperationException("Setup guide skin option is unavailable: " + part.Key + "/" + part.Value);
            }
        }
        foreach (string code in fixture.Animations)
        {
            if (!actor.Properties.Client.AnimationsByMetaCode.ContainsKey(code))
            {
                throw new InvalidOperationException("Setup guide animation is unavailable: " + code);
            }
        }
    }

    internal static EntityProperties CopyProperties(EntityProperties source, ICoreClientAPI api)
    {
        // EntityProperties.Clone changes the source Attributes to read-only; copy without touching it.
        var client = (EntityClientProperties)source.Client.Clone();
        client.BehaviorsAsJsonObj = source.Client.BehaviorsAsJsonObj
            .Where(behavior => behavior["code"].AsString() == "extraskinnable")
            .Select(behavior => new JsonObject(behavior.Token.DeepClone())).ToArray();
        client.Textures = new Dictionary<string, CompositeTexture>();
        foreach (var texture in source.Client.Textures)
        {
            var copy = texture.Value.Clone();
            copy.Bake(api.Assets);
            copy.Baked.TextureSubId = texture.Value.Baked.TextureSubId;
            client.Textures.Add(texture.Key, copy);
        }
        return new EntityProperties
        {
            Id = source.Id,
            Code = source.Code.Clone(),
            Class = "EntityPlayerBot",
            Tags = source.Tags,
            Color = source.Color,
            Habitat = source.Habitat,
            CollisionBoxSize = source.CollisionBoxSize?.Clone(),
            DeadCollisionBoxSize = source.DeadCollisionBoxSize?.Clone(),
            SelectionBoxSize = source.SelectionBoxSize?.Clone(),
            DeadSelectionBoxSize = source.DeadSelectionBoxSize?.Clone(),
            EyeHeight = source.EyeHeight,
            SwimmingEyeHeight = source.SwimmingEyeHeight,
            Weight = source.Weight,
            CanClimb = source.CanClimb,
            CanClimbAnywhere = source.CanClimbAnywhere,
            FallDamage = source.FallDamage,
            FallDamageMultiplier = source.FallDamageMultiplier,
            ClimbTouchDistance = source.ClimbTouchDistance,
            RotateModelOnClimb = source.RotateModelOnClimb,
            KnockbackResistance = source.KnockbackResistance,
            Attributes = new JsonObject(source.Attributes.Token.DeepClone()),
            Client = client,
            Sounds = new Dictionary<string, SoundAttributes>(),
            IdleSoundChance = 0,
            Variant = new Vintagestory.API.Datastructures.OrderedDictionary<string, string>(source.Variant)
        };
    }

    /// <summary>Pins a repeatable frame. Ordinary rendering advances idle until a named time is set.</summary>
    public void SetAnimation(string code, double timeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!fixture.Animations.Contains(code, StringComparer.Ordinal))
        {
            throw new ArgumentException("Unknown setup guide animation.", nameof(code));
        }
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0 || timeSeconds > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        }
        animationCode = code;
        previewTime = timeSeconds;
        namedTime = true;
        ResetAnimation();
    }

    private void ResetAnimation()
    {
        if (actor.AnimManager.Animator is not ClientAnimator animator)
        {
            return;
        }
        // Only private poses and joints are shared here. No animation manager frame, sound, or world tick.
        previewAnimator = new ClientAnimator(() => 1, animator.Animations.Select(animation => animation.Animation).ToArray(),
            animator.RootElements, animator.jointsById);
        actor.AnimManager.Animator = previewAnimator;
        var active = actor.AnimManager.ActiveAnimationsByAnimCode;
        active.Clear();
        active["idle"] = actor.Properties.Client.AnimationsByMetaCode["idle"].Clone();
        if (animationCode != "idle")
        {
            active[animationCode] = actor.Properties.Client.AnimationsByMetaCode[animationCode].Clone();
        }
        previewAnimator.OnFrame(active, 0);
        int steps = (int)Math.Floor(previewTime * 60);
        for (int step = 0; step < steps; step++)
        {
            previewAnimator.OnFrame(active, FrameStep);
        }
        frameRemainder = previewTime - steps / 60d;
        if (namedTime && frameRemainder > 0)
        {
            previewAnimator.OnFrame(active, (float)frameRemainder);
        }
    }

    public void Render(float dt, ElementBounds bounds)
    {
        if (disposed || bounds == null)
        {
            return;
        }
        if (actor.AnimManager.Animator != previewAnimator)
        {
            ResetAnimation();
        }
        if (!namedTime && previewAnimator != null && float.IsFinite(dt) && dt > 0)
        {
            frameRemainder += Math.Min(dt, 0.25f);
            while (frameRemainder >= FrameStep)
            {
                previewAnimator.OnFrame(actor.AnimManager.ActiveAnimationsByAnimCode, FrameStep);
                previewTime += FrameStep;
                frameRemainder -= FrameStep;
            }
        }
        api.Render.GlPushMatrix();
        try
        {
            api.Render.PushScissor(bounds, api.Render.ScissorStack.Count > 0 && api.Render.ScissorStack.Peek() != null);
            try
            {
                float size = (float)Math.Min(bounds.InnerWidth * 1.1, bounds.InnerHeight * 0.46);
                api.Render.RenderEntityToGui(0, actor,
                    bounds.renderX + bounds.InnerWidth / 2 - size,
                    bounds.renderY + bounds.InnerHeight * 0.96 - 2 * size,
                    100, -0.18f, size, -1);
            }
            finally
            {
                api.Render.PopScissor();
            }
        }
        finally
        {
            api.Render.GlPopMatrix();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (actor != null)
        {
            actor.Disposed = true;
            actor.OnInitialized -= AddPrivateInventory;
        }
        try
        {
            renderer?.Dispose();
        }
        finally
        {
            if (actor?.Properties != null)
            {
                try
                {
                    actor.OnEntityDespawn(new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                finally
                {
                    actor.Properties.Client.Renderer = null;
                }
            }
        }
    }

    private sealed class DetachedGuideEntity : EntityPlayerBot
    {
        public bool Disposed;
        public override bool StoreWithChunk => false;
        public override void OnGameTick(float dt) { }
        public override void OnTesselated()
        {
            if (!Disposed) base.OnTesselated();
        }
    }

    private sealed class GuideInventory : EntityBehaviorSeraphInventory
    {
        public GuideInventory(Entity entity) : base(entity) { }

        public override void OnTesselation(ref Shape shape, string path, ref bool cloned, ref string[] deleted)
        {
            // The native whole-inventory helper writes shared entity-type textures. Its per-slot helper is isolated.
            foreach (var slot in Inventory)
            {
                if (!slot.Empty)
                {
                    shape = addGearToShape(shape, slot, "default", path, ref cloned, ref deleted);
                }
            }
            reloadSkin();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            // Native clothing frees slot zero even if no skin space was allocated.
            if (skinTexPos != null) base.OnEntityDespawn(despawn);
        }
    }

    private sealed class GuideRenderer : EntityShapeRenderer
    {
        private bool disposed;
        public bool IsReady => meshRefOpaque != null;

        public GuideRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            DoRenderHeldItem = false;
            glitchAffected = false;
            frostable = false;
            shouldSwivelFromMotion = false;
        }

        public override void TesselateShape()
        {
            if (disposed) return;
            // Allocate private skin space before OnTesselation reloads appearance and clothing into it.
            var handling = EnumHandling.PassThrough;
            entity.GetBehavior<GuideInventory>().GetTextureSource(ref handling);
            base.TesselateShape();
        }

        protected override void onMeshReady(MeshData meshData)
        {
            if (!disposed) base.onMeshReady(meshData);
        }

        public override void RenderToGui(float dt, double x, double y, double z, float yaw, float size)
        {
            if (disposed) return;
            var shader = capi.Render.CurrentActiveShader;
            var originalMatrix = (float[])ModelMat.Clone();
            try
            {
                var animator = entity.AnimManager.Animator;
                if (animator != null)
                {
                    shader.UBOs["Animation"].Update((object)animator.Matrices, 0, animator.MaxJointId * 16 * sizeof(float));
                    shader.Uniform("applyAnimation", 1);
                }
                base.RenderToGui(dt, x, y, z, yaw, size);
            }
            finally
            {
                shader.Uniform("applyAnimation", 0);
                ModelMat = originalMatrix;
            }
        }

        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            base.Dispose();
        }
    }

    private sealed class GuideFixture
    {
        public string Name { get; set; }
        public Dictionary<string, string> SkinParts { get; set; }
        public GuideGarment[] Gear { get; set; }
        public string[] Animations { get; set; }
    }

    private sealed class GuideGarment
    {
        public int Slot { get; set; }
        public string Code { get; set; }
    }
}




