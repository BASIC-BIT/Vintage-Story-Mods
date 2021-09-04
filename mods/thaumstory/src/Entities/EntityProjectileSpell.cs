using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thaumstory.Entities
{
    public class EntityProjectileSpell : EntityProjectile
    {
        public float EmitPeriod = 0.05f;
        private float _timeSinceEmit = 0.05f;

        public override bool ApplyGravity
        {
            get { return false; }
        }

        protected SimpleParticleProperties InFlightParticles = new SimpleParticleProperties(2f, 3f,
            ColorUtil.ToRgba(150, 180, 180, 180), new Vec3d(), new Vec3d(),
            new Vec3f(-0.17f, -0.17f, -0.17f), new Vec3f(0.17f, 0.17f, 0.17f), 0.15f, 1f / 400f, 0.18f, 0.45f,
            EnumParticleModel.Quad)
        {
            VertexFlags = 155
        };

        protected SimpleParticleProperties ExplosionParticles = new SimpleParticleProperties(2f, 3f,
            ColorUtil.ToRgba(150, 180, 180, 180), new Vec3d(), new Vec3d(),
            new Vec3f(-0.17f, -0.17f, -0.17f), new Vec3f(0.17f, 0.17f, 0.17f), 0.5f, 1f / 400f, 0.45f, 0.75f,
            EnumParticleModel.Quad)
        {
            VertexFlags = 155
        };

        public new int RenderColor
        {
            get { return ColorUtil.ToRgba(255, 255, 255, 150); }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            InFlightParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, 0.95f);
            InFlightParticles.OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -100);
            ExplosionParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, 1.1f);
            ExplosionParticles.OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -100);

            Item item = World.GetItem(new AssetLocation("thaumstory:dummy-wand-ammo"));
            ProjectileStack = new ItemStack(item, Int32.MaxValue);

            LightHsv = new byte[3]
            {
                (byte)4,
                (byte)2,
                (byte)14
            };
        }

        protected void SpawnInFlightParticles(int times = 1)
        {
            if (this.Alive)
            {
                var random = new Random();
                InFlightParticles.MinPos = SidedPos.XYZ;

                for (var i = 0; i < times; i++)
                {
                    InFlightParticles.Color = ColorUtil.HsvToRgba(random.Next(255), random.Next(175, 255), 255, 120);
                    Api.World.SpawnParticles(InFlightParticles);
                }
            }
        }

        protected void SpawnExplosionParticles(int times = 1)
        {
            if (this.Alive)
            {
                var random = new Random();
                ExplosionParticles.MinPos = SidedPos.XYZ;

                for (var i = 0; i < times; i++)
                {
                    ExplosionParticles.Color = ColorUtil.HsvToRgba(random.Next(255), random.Next(150, 200), 255, 170);
                    Api.World.SpawnParticles(ExplosionParticles);
                }
            }
        }

        public override bool CanCollect(Entity byEntity)
        {
            return false;
        }

        public override void OnCollided()
        {
            SpawnExplosionParticles(10);
            base.OnCollided();
            if (this.Alive)
            {
                Die();
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            _timeSinceEmit += dt;
            if (_timeSinceEmit >= EmitPeriod)
            {
                _timeSinceEmit = 0;
                SpawnInFlightParticles();
            }
        }
    }
}