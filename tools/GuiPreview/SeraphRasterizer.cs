using Vintagestory.API.Client;

namespace TheBasics.GuiPreview;

/// <summary>Orthographic native mesh rendering into premultiplied BGRA pixels for Cairo.</summary>
public static class SeraphRasterizer
{
    private readonly record struct Vertex(double X, double Y, double Depth, double U, double V, double R, double G, double B, double A);
    private readonly record struct Fragment(int Pixel, double Depth, uint Color);

    public static byte[] Render(SeraphModel model, float[] joints, int width, int height, float yaw, bool fitPosed = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(joints);
        if (width is < 1 or > 1024 || height is < 1 or > 1024)
            throw new ArgumentOutOfRangeException(nameof(width), "Raster dimensions must be between 1 and 1024 pixels.");
        if (!float.IsFinite(yaw) || joints.Length is < 16 or > 65536 || joints.Length % 16 != 0 || joints.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Yaw and complete joint matrices must be finite.");
        var mesh = model.Mesh;
        Validate(mesh, model.Textures, joints.Length / 16);

        double radius = 0, low = double.PositiveInfinity, high = double.NegativeInfinity;
        for (int vertex = 0; vertex < mesh.VerticesCount; vertex++)
        {
            double x = mesh.xyz[vertex * 3] - 0.5, y = mesh.xyz[vertex * 3 + 1], z = mesh.xyz[vertex * 3 + 2] - 0.5;
            radius = Math.Max(radius, Math.Sqrt(x * x + z * z));
            low = Math.Min(low, y);
            high = Math.Max(high, y);
        }
        if (radius < 0.000001 && high - low < 0.000001) throw new ArgumentException("The mesh has no extent.", nameof(model));
        double scale = 0.9 * Math.Min(width / Math.Max(radius * 2, 0.000001), height / Math.Max(high - low, 0.000001));
        double centerY = (low + high) / 2, cosine = Math.Cos(yaw), sine = Math.Sin(yaw);
        var vertices = new Vertex[mesh.VerticesCount];
        for (int index = 0; index < vertices.Length; index++)
        {
            int point = index * 3, joint = mesh.CustomInts.Values[index] * 16;
            double x = mesh.xyz[point], y = mesh.xyz[point + 1], z = mesh.xyz[point + 2];
            double posedX = joints[joint] * x + joints[joint + 4] * y + joints[joint + 8] * z + joints[joint + 12] - 0.5;
            double posedY = joints[joint + 1] * x + joints[joint + 5] * y + joints[joint + 9] * z + joints[joint + 13];
            double posedZ = joints[joint + 2] * x + joints[joint + 6] * y + joints[joint + 10] * z + joints[joint + 14] - 0.5;
            bool colored = mesh.Rgba is { Length: > 0 };
            // Native GetModelMatrixForGui reflects centered Z before its GUI yaw and Y flip.
            vertices[index] = new(width / 2d + (cosine * posedX - sine * posedZ) * scale,
                height / 2d - (posedY - centerY) * scale, sine * posedX + cosine * posedZ,
                mesh.Uv[index * 2], mesh.Uv[index * 2 + 1],
                colored ? mesh.Rgba[index * 4] : 255, colored ? mesh.Rgba[index * 4 + 1] : 255,
                colored ? mesh.Rgba[index * 4 + 2] : 255, colored ? mesh.Rgba[index * 4 + 3] : 255);
        }

        if (fitPosed)
        {
            double left = vertices.Min(point => point.X), right = vertices.Max(point => point.X);
            double top = vertices.Min(point => point.Y), bottom = vertices.Max(point => point.Y);
            double fit = 0.9 * Math.Min(width / Math.Max(right - left, 0.000001), height / Math.Max(bottom - top, 0.000001));
            for (int index = 0; index < vertices.Length; index++)
            {
                var point = vertices[index];
                vertices[index] = point with { X = width / 2d + (point.X - (left + right) / 2) * fit,
                    Y = height / 2d + (point.Y - (top + bottom) / 2) * fit, Depth = point.Depth * fit };
            }
        }

        var pixels = new byte[width * height * 4];
        var depth = new double[width * height];
        Array.Fill(depth, double.NegativeInfinity);
        var transparent = new List<Fragment>();
        long work = 0;
        for (int triangle = 0; triangle < mesh.IndicesCount; triangle += 3)
        {
            var texture = model.Textures[mesh.TextureIds[mesh.TextureIndices[triangle / mesh.IndicesPerFace]]];
            var a = vertices[mesh.Indices[triangle]];
            var b = vertices[mesh.Indices[triangle + 1]];
            var c = vertices[mesh.Indices[triangle + 2]];
            double area = Edge(a, b, c.X, c.Y);
            if (Math.Abs(area) < 0.000001) continue;
            if (area < 0) { (b, c) = (c, b); area = -area; }
            int minX = (int)Math.Clamp(Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, width);
            int maxX = (int)Math.Clamp(Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, width);
            int minY = (int)Math.Clamp(Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, height);
            int maxY = (int)Math.Clamp(Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, height);
            work += (long)(maxX - minX) * (maxY - minY);
            if (work > 64_000_000) throw new ArgumentException("The mesh exceeds the raster work limit.", nameof(model));
            double light = Lighting(a, b, c, scale);
            for (int y = minY; y < maxY; y++)
            for (int x = minX; x < maxX; x++)
            {
                double wa = Edge(b, c, x + 0.5, y + 0.5), wb = Edge(c, a, x + 0.5, y + 0.5), wc = Edge(a, b, x + 0.5, y + 0.5);
                if (!Inside(wa, b, c) || !Inside(wb, c, a) || !Inside(wc, a, b)) continue;
                wa /= area; wb /= area; wc /= area;
                int pixel = y * width + x;
                double z = wa * a.Depth + wb * b.Depth + wc * c.Depth;
                if (z < depth[pixel]) continue;
                double u = wa * a.U + wb * b.U + wc * c.U, v = wa * a.V + wb * b.V + wc * c.V;
                int sample = ((int)Math.Clamp(v * texture.Height, 0, texture.Height - 1) * texture.Width +
                    (int)Math.Clamp(u * texture.Width, 0, texture.Width - 1)) * 4;
                int alpha = (int)Math.Round(texture.Rgba[sample + 3] * (wa * a.A + wb * b.A + wc * c.A) / 255);
                if (alpha == 0) continue;
                byte red = Channel(texture.Rgba[sample], wa * a.R + wb * b.R + wc * c.R, light, alpha);
                byte green = Channel(texture.Rgba[sample + 1], wa * a.G + wb * b.G + wc * c.G, light, alpha);
                byte blue = Channel(texture.Rgba[sample + 2], wa * a.B + wb * b.B + wc * c.B, light, alpha);
                uint color = (uint)(blue | green << 8 | red << 16 | alpha << 24);
                if (alpha == 255)
                {
                    depth[pixel] = z;
                    Write(pixels, pixel, color);
                }
                else
                {
                    if (transparent.Count >= 2_000_000) throw new ArgumentException("The mesh exceeds the transparent fragment limit.", nameof(model));
                    transparent.Add(new(pixel, z, color));
                }
            }
        }
        // Per-pixel sorting preserves intersecting translucent faces without changing opaque depth.
        transparent.Sort((a, b) => a.Pixel != b.Pixel ? a.Pixel.CompareTo(b.Pixel) : a.Depth.CompareTo(b.Depth));
        foreach (var fragment in transparent)
        {
            if (fragment.Depth < depth[fragment.Pixel]) continue;
            int offset = fragment.Pixel * 4, inverse = 255 - (int)(fragment.Color >> 24);
            for (int channel = 0; channel < 4; channel++)
                pixels[offset + channel] = (byte)((fragment.Color >> (channel * 8) & 255) + (pixels[offset + channel] * inverse + 127) / 255);
        }
        return pixels;
    }

    private static void Validate(MeshData mesh, IReadOnlyDictionary<int, SeraphTexture> textures, int joints)
    {
        if (mesh == null || textures == null || mesh.VerticesCount is < 1 or > 100_000 || mesh.IndicesCount is < 3 or > 300_000 ||
            mesh.IndicesCount % 3 != 0 || mesh.IndicesPerFace is not (3 or 6) || mesh.IndicesCount % mesh.IndicesPerFace != 0 ||
            mesh.xyz == null || mesh.xyz.Length < mesh.VerticesCount * 3 || mesh.Uv == null || mesh.Uv.Length < mesh.VerticesCount * 2 ||
            mesh.Indices == null || mesh.Indices.Length < mesh.IndicesCount || mesh.CustomInts?.Values == null || mesh.CustomInts.Values.Length < mesh.VerticesCount || mesh.CustomInts.Count < mesh.VerticesCount ||
            mesh.TextureIndices == null || mesh.TextureIndices.Length < mesh.IndicesCount / mesh.IndicesPerFace || mesh.TextureIndicesCount < mesh.IndicesCount / mesh.IndicesPerFace || mesh.TextureIds == null ||
            mesh.Rgba is { Length: > 0 } && mesh.Rgba.Length < mesh.VerticesCount * 4)
            throw new ArgumentException("The mesh buffers are incomplete.", nameof(mesh));
        for (int vertex = 0; vertex < mesh.VerticesCount; vertex++)
        {
            if (mesh.CustomInts.Values[vertex] < 0 || mesh.CustomInts.Values[vertex] >= joints)
                throw new ArgumentException("A vertex references a missing joint.", nameof(mesh));
            for (int axis = 0; axis < 3; axis++)
                if (!float.IsFinite(mesh.xyz[vertex * 3 + axis]) || Math.Abs(mesh.xyz[vertex * 3 + axis]) > 10_000)
                    throw new ArgumentException("Mesh positions must be finite and bounded.", nameof(mesh));
            for (int axis = 0; axis < 2; axis++)
                if (!float.IsFinite(mesh.Uv[vertex * 2 + axis]) || mesh.Uv[vertex * 2 + axis] is < -0.0001f or > 1.0001f)
                    throw new ArgumentException("Texture coordinates must lie inside the texture.", nameof(mesh));
        }
        for (int index = 0; index < mesh.IndicesCount; index++)
            if (mesh.Indices[index] < 0 || mesh.Indices[index] >= mesh.VerticesCount)
                throw new ArgumentException("A triangle references a missing vertex.", nameof(mesh));
        for (int face = 0; face < mesh.IndicesCount / mesh.IndicesPerFace; face++)
        {
            int texture = mesh.TextureIndices[face];
            if (texture >= mesh.TextureIds.Length || !textures.TryGetValue(mesh.TextureIds[texture], out var image) || image == null ||
                image.Width is < 1 or > 4096 || image.Height is < 1 or > 4096 || image.Rgba == null || image.Rgba.Length != (long)image.Width * image.Height * 4)
                throw new ArgumentException("A face references an incomplete texture.", nameof(mesh));
        }
    }

    private static double Edge(Vertex a, Vertex b, double x, double y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
    private static bool Inside(double edge, Vertex a, Vertex b) => edge > 0 || edge == 0 && (b.Y < a.Y || b.Y == a.Y && b.X > a.X);
    private static byte Channel(byte texture, double tint, double light, int alpha) => (byte)Math.Clamp(Math.Round(texture * tint / 255 * light * alpha / 255), 0, alpha);
    private static void Write(byte[] pixels, int pixel, uint color)
    {
        for (int channel = 0; channel < 4; channel++) pixels[pixel * 4 + channel] = (byte)(color >> (channel * 8));
    }

    private static double Lighting(Vertex a, Vertex b, Vertex c, double scale)
    {
        double ux = b.X - a.X, uy = a.Y - b.Y, uz = (b.Depth - a.Depth) * scale;
        double vx = c.X - a.X, vy = a.Y - c.Y, vz = (c.Depth - a.Depth) * scale;
        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
        double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return 0.68 + 0.32 * Math.Min(1, Math.Abs((-0.35 * nx + 0.8 * ny + 0.48 * nz) / length));
    }
}
