using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thaumstory.ParticleEffects
{
    public class MagicalExplosion : SimpleParticleProperties
    {
        private static float minQuantity = 2f;
        private static float maxQuantity = 3f;
        private static int color = ColorUtil.ToRgba(150, 180, 180, 180);
        private static Vec3d minPos = new Vec3d();
        private static Vec3d maxPos = new Vec3d();
        private static Vec3f minVelocity = new Vec3f(-0.17f, -0.17f, -0.17f);
        private static Vec3f maxVelocity = new Vec3f(0.17f, 0.17f, 0.17f); 
        private static float lifeLength = 0.5f;
        private static float gravityEffect = 1f / 400f;
        private static float minSize = 0.45f;
        private static float maxSize = 0.75f;
        private static EnumParticleModel model = EnumParticleModel.Quad;

        public MagicalExplosion()
            : base(minQuantity, maxQuantity, color, minPos, maxPos, minVelocity, maxVelocity, lifeLength, gravityEffect,
                minSize, maxSize, model)
        {
        }
    }
}