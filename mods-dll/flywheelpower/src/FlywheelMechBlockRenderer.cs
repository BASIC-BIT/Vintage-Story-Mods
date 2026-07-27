using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class FlywheelMechBlockRenderer : MechBlockRenderer
{
    private const int InstanceFloatCapacity = 202000;
    private const int WheelSegments = 72;
    private const int WheelRadialSteps = 9;
    private const float Center = 0.5f;
    private const float AxleMinX = -0.25f;
    private const float AxleMaxX = 1.25f;
    private const float TextureMeters = 0.72f;
    private const float ChalkRaise = 0.006f;
    private const float ChalkEdgeOverlap = 0.012f;
    private const float DegToRad = MathF.PI / 180f;

    private CustomMeshDataPartFloat matrixAndLightFloats;
    private MeshRef blockMeshRef;

    public FlywheelMechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc)
        : base(capi, mechanicalPowerMod)
    {
        MeshData mesh;
        if (IsFlywheelWheel(shapeLoc))
        {
            mesh = BuildFlywheelMesh(capi, textureSourceBlock, GetMeshSpec(shapeLoc), IsCoupledFlywheel(shapeLoc));
            mesh.Rotate(shapeLoc.rotateX * DegToRad, shapeLoc.rotateY * DegToRad, shapeLoc.rotateZ * DegToRad);
        }
        else
        {
            mesh = BuildShapeMesh(capi, textureSourceBlock, shapeLoc);
        }

        mesh.CustomFloats = matrixAndLightFloats = CreateInstanceFloatBuffer();
        mesh.CustomFloats.SetAllocationSize(InstanceFloatCapacity);
        blockMeshRef = capi.Render.UploadMesh(mesh);
    }

    protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
    {
        UpdateLightAndTransformMatrix(
            matrixAndLightFloats.Values,
            index,
            distToCamera,
            dev.LightRgba,
            rotation * dev.AxisSign[0],
            rotation * dev.AxisSign[1],
            rotation * dev.AxisSign[2]);
    }

    public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
    {
        UpdateCustomFloatBuffer();
        if (quantityBlocks <= 0)
        {
            return;
        }

        matrixAndLightFloats.Count = quantityBlocks * 20;
        updateMesh.CustomFloats = matrixAndLightFloats;
        capi.Render.UpdateMesh(blockMeshRef, updateMesh);
        capi.Render.RenderMeshInstanced(blockMeshRef, quantityBlocks);
    }

    public override void Dispose()
    {
        base.Dispose();
        blockMeshRef?.Dispose();
    }

    private static CustomMeshDataPartFloat CreateInstanceFloatBuffer()
    {
        return new CustomMeshDataPartFloat(InstanceFloatCapacity)
        {
            Instanced = true,
            InterleaveOffsets = new[] { 0, 16, 32, 48, 64 },
            InterleaveSizes = new[] { 4, 4, 4, 4, 4 },
            InterleaveStride = 80,
            StaticDraw = false
        };
    }

    private static bool IsFlywheelWheel(CompositeShape shapeLoc)
    {
        return shapeLoc?.Base?.Path?.Contains("flywheel-wheel", StringComparison.Ordinal) == true;
    }

    private static bool IsCoupledFlywheel(CompositeShape shapeLoc)
    {
        return shapeLoc?.Base?.Path?.Contains("coupled", StringComparison.Ordinal) == true;
    }

    private static MeshData BuildShapeMesh(ICoreClientAPI capi, Block textureSourceBlock, CompositeShape shapeLoc)
    {
        AssetLocation shapePath = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        Shape shape = Shape.TryGet(capi, shapePath);
        Vec3f meshRotationDeg = new(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
        capi.Tesselator.TesselateShape(textureSourceBlock, shape, out MeshData mesh, meshRotationDeg, shapeLoc.QuantityElements, shapeLoc.SelectiveElements);

        if (shapeLoc.Overlays == null)
        {
            return mesh;
        }

        foreach (CompositeShape overlay in shapeLoc.Overlays)
        {
            AssetLocation overlayPath = overlay.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape overlayShape = Shape.TryGet(capi, overlayPath);
            Vec3f overlayRotationDeg = new(overlay.rotateX, overlay.rotateY, overlay.rotateZ);
            capi.Tesselator.TesselateShape(textureSourceBlock, overlayShape, out MeshData overlayMesh, overlayRotationDeg);
            mesh.AddMeshData(overlayMesh);
        }

        return mesh;
    }

    private static FlywheelMeshSpec GetMeshSpec(CompositeShape shapeLoc)
    {
        if (shapeLoc?.Base?.Path?.Contains("compact", StringComparison.Ordinal) == true)
        {
            return FlywheelMeshSpec.Compact();
        }

        return FlywheelMeshSpec.FullSize();
    }

    private static MeshData BuildFlywheelMesh(ICoreClientAPI capi, Block block, FlywheelMeshSpec spec, bool coupled)
    {
        MeshData mesh = new(9000, 14000, withNormals: false, withUv: true, withRgba: true, withFlags: true);
        TextureAtlasPosition wheelTex = capi.BlockTextureAtlas.GetPosition(block, "wheel");
        TextureAtlasPosition woodTex = capi.BlockTextureAtlas.GetPosition(block, "wood");
        TextureAtlasPosition metalTex = capi.BlockTextureAtlas.GetPosition(block, "metal");
        TextureAtlasPosition bearingTex = capi.BlockTextureAtlas.GetPosition(block, "bearing");
        TextureAtlasPosition chalkTex = capi.BlockTextureAtlas.GetPosition(block, "chalk");

        float wheelMinX = Center - spec.WheelHalfThickness;
        float wheelMaxX = Center + spec.WheelHalfThickness;

        if (coupled)
        {
            if (spec.IsCompact)
            {
                AddAnnularCylinder(mesh, wheelTex, new(spec.CoupledInnerRadius, spec.WheelOuterRadius, wheelMinX, wheelMaxX, WheelSegments, WheelRadialSteps, IncludeInnerSide: false));
            }
            else
            {
                AddSpokedWeb(
                    mesh,
                    woodTex,
                    spec,
                    wheelMinX + FlywheelModelDimensions.SpokeDepthInset,
                    wheelMaxX - FlywheelModelDimensions.SpokeDepthInset);
                AddAnnularCylinder(mesh, woodTex, new(spec.FelloeInnerRadius, spec.FelloeOuterRadius, wheelMinX, wheelMaxX, WheelSegments, 2, IncludeInnerSide: true));
                AddAnnularCylinder(mesh, wheelTex, new(spec.TyreInnerRadius, spec.WheelOuterRadius, wheelMinX, wheelMaxX, WheelSegments, 2, IncludeInnerSide: true));
            }

            AddCoupledHubAssembly(mesh, metalTex, bearingTex, spec, wheelMinX, wheelMaxX);
        }
        else
        {
            AddAnnularCylinder(mesh, wheelTex, new(0f, spec.WheelOuterRadius, wheelMinX, wheelMaxX, WheelSegments, WheelRadialSteps, IncludeInnerSide: false));
            AddAnnularCylinder(mesh, metalTex, new(0f, spec.KeyedHubOuterRadius, Center - spec.HubHalfThickness, Center + spec.HubHalfThickness, WheelSegments, 3, IncludeInnerSide: false));
            AddAnnularCylinder(mesh, metalTex, new(0f, spec.AxleRadius, spec.AxleMinX, spec.AxleMaxX, 32, 2, IncludeInnerSide: false));
        }

        AddChalkLine(mesh, chalkTex, wheelMaxX + ChalkRaise, spec.WheelOuterRadius * 0.18f, spec.WheelOuterRadius + ChalkEdgeOverlap, spec.ChalkHalfWidth, frontFace: true);
        AddChalkLine(mesh, chalkTex, wheelMinX - ChalkRaise, spec.WheelOuterRadius * 0.18f, spec.WheelOuterRadius + ChalkEdgeOverlap, spec.ChalkHalfWidth, frontFace: false);
        AddChalkRimLine(mesh, chalkTex, spec.WheelOuterRadius + ChalkRaise, wheelMinX - ChalkEdgeOverlap, wheelMaxX + ChalkEdgeOverlap, spec.ChalkHalfWidth);
        return mesh;
    }

    private static void AddSpokedWeb(
        MeshData mesh,
        TextureAtlasPosition woodTex,
        FlywheelMeshSpec spec,
        float minX,
        float maxX)
    {
        for (int spoke = 0; spoke < spec.SpokeCount; spoke++)
        {
            float angle = GameMath.TWOPI * spoke / spec.SpokeCount;
            AddSpoke(
                mesh,
                woodTex,
                minX,
                maxX,
                spec.HubOuterRadius * 0.92f,
                spec.FelloeInnerRadius + 0.02f,
                spec.SpokeHalfWidth,
                angle);
        }
    }

    private static void AddSpoke(
        MeshData mesh,
        TextureAtlasPosition tex,
        float minX,
        float maxX,
        float innerRadius,
        float outerRadius,
        float halfWidth,
        float angle)
    {
        float radialY = MathF.Sin(angle);
        float radialZ = MathF.Cos(angle);
        float tangentY = MathF.Cos(angle);
        float tangentZ = -MathF.Sin(angle);

        MeshVertex fInnerLeft = SpokeVertex(maxX, innerRadius, -halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(0f, 0f));
        MeshVertex fInnerRight = SpokeVertex(maxX, innerRadius, halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(1f, 0f));
        MeshVertex fOuterRight = SpokeVertex(maxX, outerRadius, halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(1f, 1f));
        MeshVertex fOuterLeft = SpokeVertex(maxX, outerRadius, -halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(0f, 1f));
        MeshVertex bInnerLeft = SpokeVertex(minX, innerRadius, -halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(0f, 0f));
        MeshVertex bInnerRight = SpokeVertex(minX, innerRadius, halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(1f, 0f));
        MeshVertex bOuterRight = SpokeVertex(minX, outerRadius, halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(1f, 1f));
        MeshVertex bOuterLeft = SpokeVertex(minX, outerRadius, -halfWidth, radialY, radialZ, tangentY, tangentZ, new Vec2f(0f, 1f));

        AddQuad(mesh, tex, fInnerLeft, fInnerRight, fOuterRight, fOuterLeft, new Vec3f(1f, 0f, 0f));
        AddQuad(mesh, tex, bInnerLeft, bOuterLeft, bOuterRight, bInnerRight, new Vec3f(-1f, 0f, 0f));
        AddQuad(mesh, tex, fInnerRight, bInnerRight, bOuterRight, fOuterRight, new Vec3f(0f, tangentY, tangentZ));
        AddQuad(mesh, tex, fInnerLeft, fOuterLeft, bOuterLeft, bInnerLeft, new Vec3f(0f, -tangentY, -tangentZ));
        AddQuad(mesh, tex, fOuterLeft, fOuterRight, bOuterRight, bOuterLeft, new Vec3f(0f, radialY, radialZ));
        AddQuad(mesh, tex, fInnerLeft, bInnerLeft, bInnerRight, fInnerRight, new Vec3f(0f, -radialY, -radialZ));
    }

    private static MeshVertex SpokeVertex(
        float x,
        float radius,
        float tangentOffset,
        float radialY,
        float radialZ,
        float tangentY,
        float tangentZ,
        Vec2f uv)
    {
        return new MeshVertex(
            x,
            Center + radius * radialY + tangentOffset * tangentY,
            Center + radius * radialZ + tangentOffset * tangentZ,
            uv.X,
            uv.Y);
    }

    private static void AddCoupledHubAssembly(MeshData mesh, TextureAtlasPosition metalTex, TextureAtlasPosition bearingTex, FlywheelMeshSpec spec, float wheelMinX, float wheelMaxX)
    {
        float shaftClearanceRadius = Math.Max(spec.ShaftClearanceRadius, spec.AxleRadius * 1.01f);
        float bearingOuterRadius = GameMath.Clamp(spec.BearingOuterRadius, shaftClearanceRadius + 0.02f, spec.HubOuterRadius - 0.02f);
        float plateOuterRadius = GameMath.Clamp(spec.CouplingPlateOuterRadius, spec.HubOuterRadius + 0.01f, spec.WheelOuterRadius - 0.02f);
        float plateGap = Math.Min(ChalkRaise * 2f, spec.CouplingPlateThickness * 0.4f);

        AddAnnularCylinder(mesh, bearingTex, new(shaftClearanceRadius, bearingOuterRadius, Center - spec.BearingHalfThickness, Center + spec.BearingHalfThickness, 48, 2, IncludeInnerSide: true));
        AddAnnularCylinder(mesh, metalTex, new(bearingOuterRadius, spec.HubOuterRadius, Center - spec.HubHalfThickness, Center + spec.HubHalfThickness, WheelSegments, 2, IncludeInnerSide: true));
        AddAnnularCylinder(mesh, metalTex, new(shaftClearanceRadius, plateOuterRadius, wheelMaxX + plateGap, wheelMaxX + plateGap + spec.CouplingPlateThickness, WheelSegments, 3, IncludeInnerSide: true));
        AddAnnularCylinder(mesh, metalTex, new(shaftClearanceRadius, plateOuterRadius, wheelMinX - plateGap - spec.CouplingPlateThickness, wheelMinX - plateGap, WheelSegments, 3, IncludeInnerSide: true));
    }

    private static void AddAnnularCylinder(MeshData mesh, TextureAtlasPosition tex, AnnularCylinderSpec spec)
    {
        float radiusSpan = spec.OuterRadius - spec.InnerRadius;
        for (int radial = 0; radial < spec.RadialSteps; radial++)
        {
            float r0 = spec.InnerRadius + radiusSpan * radial / spec.RadialSteps;
            float r1 = spec.InnerRadius + radiusSpan * (radial + 1) / spec.RadialSteps;
            for (int segment = 0; segment < spec.Segments; segment++)
            {
                float a0 = GameMath.TWOPI * segment / spec.Segments;
                float a1 = GameMath.TWOPI * (segment + 1) / spec.Segments;
                AddDiscCell(mesh, tex, spec.MaxX, r0, r1, a0, a1, frontFace: true);
                AddDiscCell(mesh, tex, spec.MinX, r0, r1, a1, a0, frontFace: false);
            }
        }

        AddRadiusSide(mesh, tex, spec.OuterRadius, spec.MinX, spec.MaxX, spec.Segments, outerSide: true);
        if (spec.IncludeInnerSide && spec.InnerRadius > 0f)
        {
            AddRadiusSide(mesh, tex, spec.InnerRadius, spec.MaxX, spec.MinX, spec.Segments, outerSide: false);
        }
    }

    private static void AddDiscCell(MeshData mesh, TextureAtlasPosition tex, float x, float r0, float r1, float a0, float a1, bool frontFace)
    {
        MeshVertex v0 = DiscVertex(x, r0, a0);
        MeshVertex v1 = DiscVertex(x, r1, a0);
        MeshVertex v2 = DiscVertex(x, r1, a1);
        MeshVertex v3 = DiscVertex(x, r0, a1);
        ApplyPlanarUv(ref v0, ref v1, ref v2, ref v3);

        if (frontFace)
        {
            AddQuad(mesh, tex, v0, v1, v2, v3, new Vec3f(1f, 0f, 0f));
        }
        else
        {
            AddQuad(mesh, tex, v0, v3, v2, v1, new Vec3f(-1f, 0f, 0f));
        }
    }

    private static void AddRadiusSide(MeshData mesh, TextureAtlasPosition tex, float radius, float minX, float maxX, int segments, bool outerSide)
    {
        float axialLength = Math.Abs(maxX - minX);
        int axialSteps = Math.Max(1, (int)MathF.Ceiling(axialLength / TextureMeters));
        int angularSteps = Math.Max(1, (int)MathF.Ceiling(GameMath.TWOPI * radius / TextureMeters));
        float maxSegmentAngle = GameMath.TWOPI / segments;

        for (int axial = 0; axial < axialSteps; axial++)
        {
            float x0 = minX + (maxX - minX) * axial / axialSteps;
            float x1 = minX + (maxX - minX) * (axial + 1) / axialSteps;
            float v0 = 0f;
            float v1 = Math.Abs(x1 - x0) / TextureMeters;

            for (int angular = 0; angular < angularSteps; angular++)
            {
                float cellA0 = GameMath.TWOPI * angular / angularSteps;
                float cellA1 = GameMath.TWOPI * (angular + 1) / angularSteps;
                int subSegments = Math.Max(1, (int)MathF.Ceiling((cellA1 - cellA0) / maxSegmentAngle));

                for (int sub = 0; sub < subSegments; sub++)
                {
                    float u0 = (float)sub / subSegments;
                    float u1 = (float)(sub + 1) / subSegments;
                    float a0 = cellA0 + (cellA1 - cellA0) * u0;
                    float a1 = cellA0 + (cellA1 - cellA0) * u1;

                    MeshVertex vtx0 = CylinderVertex(x0, radius, a0, u0, v0);
                    MeshVertex vtx1 = CylinderVertex(x1, radius, a0, u0, v1);
                    MeshVertex vtx2 = CylinderVertex(x1, radius, a1, u1, v1);
                    MeshVertex vtx3 = CylinderVertex(x0, radius, a1, u1, v0);

                    if (outerSide)
                    {
                        AddQuad(mesh, tex, vtx0, vtx1, vtx2, vtx3, RadialNormal((a0 + a1) / 2f, 1f));
                    }
                    else
                    {
                        AddQuad(mesh, tex, vtx0, vtx3, vtx2, vtx1, RadialNormal((a0 + a1) / 2f, -1f));
                    }
                }
            }
        }
    }

    private static void AddChalkRimLine(MeshData mesh, TextureAtlasPosition tex, float radius, float minX, float maxX, float halfWidth)
    {
        float halfAngle = halfWidth / radius;
        MeshVertex a = CylinderVertex(minX, radius, -halfAngle, 0f, 0f);
        MeshVertex b = CylinderVertex(maxX, radius, -halfAngle, 0f, 1f);
        MeshVertex c = CylinderVertex(maxX, radius, halfAngle, 1f, 1f);
        MeshVertex d = CylinderVertex(minX, radius, halfAngle, 1f, 0f);
        AddQuad(mesh, tex, a, b, c, d, RadialNormal(0f, 1f));
    }

    private static void AddChalkLine(MeshData mesh, TextureAtlasPosition tex, float x, float innerRadius, float outerRadius, float halfWidth, bool frontFace)
    {
        MeshVertex a = new(x, Center - halfWidth, Center + innerRadius, 0f, 1f);
        MeshVertex b = new(x, Center + halfWidth, Center + innerRadius, 1f, 1f);
        MeshVertex c = new(x, Center + halfWidth, Center + outerRadius, 1f, 0f);
        MeshVertex d = new(x, Center - halfWidth, Center + outerRadius, 0f, 0f);
        ApplyPlanarUv(ref a, ref b, ref c, ref d);

        if (frontFace)
        {
            AddQuad(mesh, tex, a, b, c, d, new Vec3f(1f, 0f, 0f));
        }
        else
        {
            AddQuad(mesh, tex, a, d, c, b, new Vec3f(-1f, 0f, 0f));
        }
    }

    private static MeshVertex DiscVertex(float x, float radius, float angle)
    {
        float y = Center + radius * MathF.Sin(angle);
        float z = Center + radius * MathF.Cos(angle);
        return new MeshVertex(x, y, z, 0f, 0f);
    }

    private static MeshVertex CylinderVertex(float x, float radius, float angle, float u, float v)
    {
        return new MeshVertex(x, Center + radius * MathF.Sin(angle), Center + radius * MathF.Cos(angle), u, v);
    }

    private static void ApplyPlanarUv(ref MeshVertex a, ref MeshVertex b, ref MeshVertex c, ref MeshVertex d)
    {
        float minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        float minZ = Math.Min(Math.Min(a.Z, b.Z), Math.Min(c.Z, d.Z));
        float tileY = MathF.Floor(minY / TextureMeters);
        float tileZ = MathF.Floor(minZ / TextureMeters);

        a = a.WithUv(a.Y / TextureMeters - tileY, a.Z / TextureMeters - tileZ);
        b = b.WithUv(b.Y / TextureMeters - tileY, b.Z / TextureMeters - tileZ);
        c = c.WithUv(c.Y / TextureMeters - tileY, c.Z / TextureMeters - tileZ);
        d = d.WithUv(d.Y / TextureMeters - tileY, d.Z / TextureMeters - tileZ);
    }

    private static Vec3f RadialNormal(float angle, float direction)
    {
        return new Vec3f(0f, MathF.Sin(angle) * direction, MathF.Cos(angle) * direction);
    }

    private static void AddQuad(MeshData mesh, TextureAtlasPosition tex, MeshVertex a, MeshVertex b, MeshVertex c, MeshVertex d, Vec3f normal)
    {
        int vertexStart = mesh.VerticesCount;
        mesh.AddTextureId(tex.atlasTextureId);
        AddVertex(mesh, tex, a, normal);
        AddVertex(mesh, tex, b, normal);
        AddVertex(mesh, tex, c, normal);
        AddVertex(mesh, tex, d, normal);
        mesh.AddQuadIndices(vertexStart);
    }

    private static void AddVertex(MeshData mesh, TextureAtlasPosition tex, MeshVertex vertex, Vec3f normal)
    {
        float u = tex.x1 + GameMath.Clamp(vertex.U, 0f, 1f) * (tex.x2 - tex.x1);
        float v = tex.y1 + GameMath.Clamp(vertex.V, 0f, 1f) * (tex.y2 - tex.y1);
        mesh.AddWithFlagsVertex(vertex.X, vertex.Y, vertex.Z, u, v, -1, VertexFlags.PackNormal(normal));
    }

    private readonly struct MeshVertex
    {
        public MeshVertex(float x, float y, float z, float u, float v)
        {
            X = x;
            Y = y;
            Z = z;
            U = u;
            V = v;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float U { get; }
        public float V { get; }

        public MeshVertex WithUv(float u, float v)
        {
            return new MeshVertex(X, Y, Z, u, v);
        }
    }

    private readonly record struct AnnularCylinderSpec(float InnerRadius, float OuterRadius, float MinX, float MaxX, int Segments, int RadialSteps, bool IncludeInnerSide);

    private readonly struct FlywheelMeshSpec
    {
        public static FlywheelMeshSpec Compact()
        {
            return new()
            {
                WheelOuterRadius = FlywheelModelDimensions.CompactWheelOuterRadius,
                CoupledInnerRadius = FlywheelModelDimensions.CompactCoupledInnerRadius,
                HubOuterRadius = FlywheelModelDimensions.CompactHubOuterRadius,
                KeyedHubOuterRadius = FlywheelModelDimensions.CompactHubOuterRadius,
                WheelHalfThickness = FlywheelModelDimensions.CompactWheelHalfThickness,
                HubHalfThickness = FlywheelModelDimensions.CompactHubHalfThickness,
                BearingOuterRadius = FlywheelModelDimensions.CompactBearingOuterRadius,
                BearingHalfThickness = FlywheelModelDimensions.CompactBearingHalfThickness,
                AxleRadius = FlywheelModelDimensions.CompactAxleRadius,
                AxleMinX = -0.08f,
                AxleMaxX = 1.08f,
                ChalkHalfWidth = 0.025f,
                ShaftClearanceRadius = FlywheelModelDimensions.CompactShaftClearanceRadius,
                CouplingPlateOuterRadius = FlywheelModelDimensions.CompactCouplingPlateOuterRadius,
                CouplingPlateThickness = FlywheelModelDimensions.CompactCouplingPlateThickness,
                IsCompact = true
            };
        }

        public static FlywheelMeshSpec FullSize()
        {
            return new()
            {
                WheelOuterRadius = FlywheelModelDimensions.WheelOuterRadius,
                CoupledInnerRadius = FlywheelModelDimensions.CoupledInnerRadius,
                HubOuterRadius = FlywheelModelDimensions.HubOuterRadius,
                KeyedHubOuterRadius = FlywheelModelDimensions.KeyedHubOuterRadius,
                WheelHalfThickness = FlywheelModelDimensions.WheelHalfThickness,
                HubHalfThickness = FlywheelModelDimensions.HubHalfThickness,
                BearingOuterRadius = FlywheelModelDimensions.BearingOuterRadius,
                BearingHalfThickness = FlywheelModelDimensions.BearingHalfThickness,
                AxleRadius = FlywheelModelDimensions.AxleRadius,
                AxleMinX = FlywheelMechBlockRenderer.AxleMinX,
                AxleMaxX = FlywheelMechBlockRenderer.AxleMaxX,
                ChalkHalfWidth = 0.04f,
                ShaftClearanceRadius = FlywheelModelDimensions.ShaftClearanceRadius,
                CouplingPlateOuterRadius = FlywheelModelDimensions.CouplingPlateOuterRadius,
                CouplingPlateThickness = FlywheelModelDimensions.CouplingPlateThickness,
                TyreInnerRadius = FlywheelModelDimensions.TyreInnerRadius,
                FelloeInnerRadius = FlywheelModelDimensions.FelloeInnerRadius,
                FelloeOuterRadius = FlywheelModelDimensions.FelloeOuterRadius,
                SpokeHalfWidth = FlywheelModelDimensions.SpokeHalfWidth,
                SpokeCount = FlywheelModelDimensions.SpokeCount
            };
        }

        public float WheelOuterRadius { get; init; }
        public float CoupledInnerRadius { get; init; }
        public float HubOuterRadius { get; init; }
        public float KeyedHubOuterRadius { get; init; }
        public float WheelHalfThickness { get; init; }
        public float HubHalfThickness { get; init; }
        public float BearingOuterRadius { get; init; }
        public float BearingHalfThickness { get; init; }
        public float AxleRadius { get; init; }
        public float AxleMinX { get; init; }
        public float AxleMaxX { get; init; }
        public float ChalkHalfWidth { get; init; }
        public float ShaftClearanceRadius { get; init; }
        public float CouplingPlateOuterRadius { get; init; }
        public float CouplingPlateThickness { get; init; }
        public bool IsCompact { get; init; }
        public float TyreInnerRadius { get; init; }
        public float FelloeInnerRadius { get; init; }
        public float FelloeOuterRadius { get; init; }
        public float SpokeHalfWidth { get; init; }
        public int SpokeCount { get; init; }
    }
}
