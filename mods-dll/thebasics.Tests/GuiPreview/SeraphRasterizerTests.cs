using TheBasics.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.GuiPreview;

public class SeraphRasterizerTests
{
    [Fact]
    public void TextureCoordinatesReachAllFourCornersWithoutFillingTheMargin()
    {
        var model = Model(Quad(0, 1, 0.5f, 0), new SeraphTexture(2, 2,
            [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255]));
        var pixels = SeraphRasterizer.Render(model, Identity(), 32, 32, 0);
        AssertColor(pixels, 8, 8, red: true);
        AssertColor(pixels, 24, 8, green: true);
        AssertColor(pixels, 8, 24, blue: true);
        var yellow = Pixel(pixels, 24, 24);
        Assert.True(yellow[1] > 100 && yellow[2] > 100);
        Assert.Equal(0, yellow[0]);
        Assert.Equal(255, yellow[3]);
        Assert.Equal(new byte[4], Pixel(pixels, 0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NearOpaqueFaceWinsRegardlessOfTriangleOrder(bool nearFirst)
    {
        var near = Quad(0, 1, 0.75f, 0);
        var far = Quad(0, 1, 0.25f, 1);
        var mesh = nearFirst ? Join(near, far) : Join(far, near);
        var model = new SeraphModel(mesh, new Dictionary<int, SeraphTexture> { [0] = Solid(255, 0, 0), [1] = Solid(0, 0, 255) });
        AssertColor(SeraphRasterizer.Render(model, Identity(), 32, 32, 0), 16, 16, red: true);
    }

    [Fact]
    public void TransparentTexelsLeaveBothColorAndDepthUntouched()
    {
        var model = new SeraphModel(Join(Quad(0, 1, 0.75f, 0), Quad(0, 1, 0.25f, 1)),
            new Dictionary<int, SeraphTexture>
            {
                [0] = new(2, 1, [255, 0, 0, 0, 0, 255, 0, 255]),
                [1] = Solid(0, 0, 255)
            });
        var pixels = SeraphRasterizer.Render(model, Identity(), 32, 32, 0);
        AssertColor(pixels, 8, 16, blue: true);
        AssertColor(pixels, 24, 16, green: true);
    }

    [Fact]
    public void PartialAlphaIsPremultipliedAndSortedBehindNearerFragments()
    {
        var red = new SeraphModel(Quad(0, 1, 0.25f, 0), new Dictionary<int, SeraphTexture> { [0] = Solid(255, 0, 0) });
        var opaque = Pixel(SeraphRasterizer.Render(red, Identity(), 32, 32, 0), 16, 16);
        var partial = Model(Quad(0, 1, 0.25f, 0), Solid(255, 0, 0, 128));
        var sample = Pixel(SeraphRasterizer.Render(partial, Identity(), 32, 32, 0), 16, 16);
        Assert.Equal(128, sample[3]);
        Assert.InRange(sample[2], opaque[2] * 128 / 255 - 1, opaque[2] * 128 / 255 + 1);
        Assert.Equal(0, sample[0]);
        Assert.Equal(0, sample[1]);

        byte[] Render(bool nearFirst)
        {
            var near = Quad(0, 1, 0.75f, 0);
            var far = Quad(0, 1, 0.25f, 1);
            return SeraphRasterizer.Render(new SeraphModel(nearFirst ? Join(near, far) : Join(far, near),
                new Dictionary<int, SeraphTexture> { [0] = Solid(255, 0, 0, 128), [1] = Solid(0, 0, 255, 128) }), Identity(), 32, 32, 0);
        }
        var first = Render(true);
        Assert.Equal(first, Render(false));
        var blended = Pixel(first, 16, 16);
        Assert.Equal(192, blended[3]);
        Assert.True(blended[2] > blended[0] && blended[0] > 0);
    }

    [Fact]
    public void EachVertexUsesItsOwnColumnMajorJointMatrixWithoutMovingTheFrame()
    {
        var mesh = Join(Quad(0, 0.4f, 0.5f, 0), Quad(0.6f, 1, 0.5f, 1));
        Array.Fill(mesh.CustomInts.Values, 1, 4, 4);
        var model = new SeraphModel(mesh, new Dictionary<int, SeraphTexture> { [0] = Solid(255, 0, 0), [1] = Solid(0, 255, 0) });
        var joints = Identity().Concat(Identity()).ToArray();
        var before = SeraphRasterizer.Render(model, joints, 32, 32, 0);
        AssertColor(before, 25, 26, green: true);
        joints[16 + 13] = 0.25f;
        var after = SeraphRasterizer.Render(model, joints, 32, 32, 0);
        Assert.Equal(new byte[4], Pixel(after, 25, 26));
        AssertColor(after, 6, 26, red: true);
        AssertColor(after, 25, 12, green: true);
    }

    [Fact]
    public void YawRotatesTextureOrientationAndQuarterTurnMakesAPlaneEdgeOn()
    {
        var model = Model(Quad(0, 1, 0.5f, 0), new SeraphTexture(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]));
        AssertColor(SeraphRasterizer.Render(model, Identity(), 32, 32, 0), 8, 16, red: true);
        AssertColor(SeraphRasterizer.Render(model, Identity(), 32, 32, MathF.PI), 8, 16, green: true);
        var edge = SeraphRasterizer.Render(model, Identity(), 32, 32, MathF.PI / 2);
        Assert.All(edge.Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
    }

    [Fact]
    public void VertexTintMultipliesTextureColorAndAlpha()
    {
        var mesh = Quad(0, 1, 0.5f, 0);
        mesh.Rgba = [128, 255, 255, 128, 128, 255, 255, 128, 128, 255, 255, 128, 128, 255, 255, 128];
        var pixel = Pixel(SeraphRasterizer.Render(Model(mesh, Solid(255, 255, 255, 128)), Identity(), 32, 32, 0), 16, 16);
        Assert.Equal(64, pixel[3]);
        Assert.Equal(pixel[0], pixel[1]);
        Assert.InRange(pixel[2], pixel[1] / 2 - 1, pixel[1] / 2 + 1);
    }

    [Fact]
    public void NativeGuiReflectsZBeforeTurningAnAsymmetricPart()
    {
        var mesh = Quad(0, 1, 0.5f, 0);
        mesh.xyz = [0.5f, 0, 0.6f, 0.5f, 0, 1, 0.5f, 1, 1, 0.5f, 1, 0.6f];
        var model = Model(mesh, Solid(255, 0, 0));
        var rightTurn = SeraphRasterizer.Render(model, Identity(), 32, 32, MathF.PI / 2);
        AssertColor(rightTurn, 8, 16, red: true);
        Assert.Equal(new byte[4], Pixel(rightTurn, 24, 16));
        var leftTurn = SeraphRasterizer.Render(model, Identity(), 32, 32, -MathF.PI / 2);
        AssertColor(leftTurn, 24, 16, red: true);
        Assert.Equal(new byte[4], Pixel(leftTurn, 8, 16));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(32, -1)]
    [InlineData(1025, 32)]
    public void InvalidOutputSizesFailBeforeAllocating(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SeraphRasterizer.Render(Model(Quad(0, 1, 0.5f, 0), Solid(255, 0, 0)), Identity(), width, height, 0));

    [Fact]
    public void MalformedBuffersJointIdsTextureReferencesAndNonFiniteInputsFail()
    {
        void Reject(Action<MeshData> corrupt)
        {
            var mesh = Quad(0, 1, 0.5f, 0);
            corrupt(mesh);
            Assert.Throws<ArgumentException>(() => SeraphRasterizer.Render(Model(mesh, Solid(255, 0, 0)), Identity(), 32, 32, 0));
        }
        Reject(mesh => mesh.Indices[0] = 4);
        Reject(mesh => mesh.xyz[0] = float.NaN);
        Reject(mesh => mesh.Uv = []);
        Reject(mesh => mesh.CustomInts.Values[0] = 1);
        Reject(mesh => mesh.CustomInts.Count = 0);
        Reject(mesh => mesh.TextureIndices[0] = 1);
        var model = Model(Quad(0, 1, 0.5f, 0), Solid(255, 0, 0));
        Assert.Throws<ArgumentException>(() => SeraphRasterizer.Render(model, new float[15], 32, 32, 0));
        Assert.Throws<ArgumentException>(() => SeraphRasterizer.Render(model, Identity(), 32, 32, float.NaN));
        Assert.Throws<ArgumentException>(() => SeraphRasterizer.Render(Model(Quad(0, 1, 0.5f, 0), new SeraphTexture(2, 1, [255, 0, 0, 255])), Identity(), 32, 32, 0));
    }

    private static SeraphModel Model(MeshData mesh, SeraphTexture texture) => new(mesh, new Dictionary<int, SeraphTexture> { [0] = texture });
    private static SeraphTexture Solid(byte red, byte green, byte blue, byte alpha = 255) => new(1, 1, [red, green, blue, alpha]);
    private static float[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
    private static byte[] Pixel(byte[] pixels, int x, int y) => pixels[((y * 32 + x) * 4)..((y * 32 + x + 1) * 4)];
    private static void AssertColor(byte[] pixels, int x, int y, bool red = false, bool green = false, bool blue = false)
    {
        var pixel = Pixel(pixels, x, y);
        Assert.Equal(255, pixel[3]);
        Assert.Equal(blue, pixel[0] > 100);
        Assert.Equal(green, pixel[1] > 100);
        Assert.Equal(red, pixel[2] > 100);
    }

    private static MeshData Quad(float left, float right, float z, int textureId) => new()
    {
        VerticesCount = 4, IndicesCount = 6, IndicesPerFace = 6, TextureIndicesCount = 1,
        xyz = [left, 0, z, right, 0, z, right, 1, z, left, 1, z],
        Uv = [0, 1, 1, 1, 1, 0, 0, 0], Indices = [0, 1, 2, 0, 2, 3],
        CustomInts = new CustomMeshDataPartInt { Values = [0, 0, 0, 0], Count = 4 },
        TextureIds = [textureId], TextureIndices = [0]
    };

    private static MeshData Join(MeshData first, MeshData second) => new()
    {
        VerticesCount = 8, IndicesCount = 12, IndicesPerFace = 6, TextureIndicesCount = 2,
        xyz = first.xyz.Concat(second.xyz).ToArray(), Uv = first.Uv.Concat(second.Uv).ToArray(),
        Indices = first.Indices.Concat(second.Indices.Select(index => index + 4)).ToArray(),
        CustomInts = new CustomMeshDataPartInt { Values = first.CustomInts.Values.Concat(second.CustomInts.Values).ToArray(), Count = 8 },
        TextureIds = [first.TextureIds[0], second.TextureIds[0]], TextureIndices = [0, 1]
    };
}
