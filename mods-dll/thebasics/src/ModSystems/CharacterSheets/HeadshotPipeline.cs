using System;
using System.IO;
using System.Security.Cryptography;
using SkiaSharp;
using thebasics.Models;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.CharacterSheets;

/// <summary>
/// Stable identifiers for the failure modes <see cref="HeadshotPipeline.Normalize"/> can return.
/// </summary>
public static class HeadshotErrorCodes
{
    public const string Empty = "empty";
    public const string OptionsNull = "options-null";
    public const string BadOptions = "bad-options";
    public const string UnsupportedFormat = "unsupported-format";
    public const string DimensionsExceeded = "dimensions-exceeded";
    public const string DecodeCreateFailed = "decode-create-failed";
    public const string DecodeZeroDims = "decode-zero-dims";
    public const string DecodeFailed = "decode-failed";
    public const string CropFailed = "crop-failed";
    public const string TargetZero = "target-zero";
    public const string ResizeFailed = "resize-failed";
    public const string ImageFailed = "image-failed";
    public const string EncodeFailed = "encode-failed";
    public const string OutputTooLarge = "output-too-large";
    public const string Exception = "exception";
}

/// <summary>
/// Shared image-normalization pipeline: validates incoming bytes (PNG/JPEG only),
/// rejects oversize / decompression-bomb inputs, center-crops to square, resizes to a
/// target dimension, and re-encodes as PNG. Used both client-side (before upload, to
/// minimize wire bytes) and server-side (defense-in-depth, since clients aren't trusted).
/// </summary>
public static class HeadshotPipeline
{
    private const int PngEncodeQuality = 90;

    public sealed record NormalizeOptions(
        int TargetDimension,
        int MaxOutputBytes,
        int MaxDecodedDimensionEitherAxis);

    public sealed record NormalizeResult(
        bool Ok,
        string ErrorCode,
        byte[] PngBytes,
        string Hash,
        int Width,
        int Height);

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // The variant byte (E0/E1/...) varies by JPEG flavor; checking the constant prefix is enough for sniffing.
    private static readonly byte[] JpegMagicPrefix = { 0xFF, 0xD8, 0xFF };

    public static NormalizeResult Normalize(byte[] inputBytes, NormalizeOptions options)
    {
        if (options == null)
        {
            return Fail(HeadshotErrorCodes.OptionsNull);
        }

        if (inputBytes == null || inputBytes.Length == 0)
        {
            return Fail(HeadshotErrorCodes.Empty);
        }

        if (!IsPng(inputBytes) && !IsJpeg(inputBytes))
        {
            return Fail(HeadshotErrorCodes.UnsupportedFormat);
        }

        if (options.TargetDimension <= 0 || options.MaxOutputBytes <= 0 || options.MaxDecodedDimensionEitherAxis <= 0)
        {
            return Fail(HeadshotErrorCodes.BadOptions);
        }

        try
        {
            using var codec = SKCodec.Create(new MemoryStream(inputBytes, writable: false));
            if (codec == null)
            {
                return Fail(HeadshotErrorCodes.DecodeCreateFailed);
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
            {
                return Fail(HeadshotErrorCodes.DecodeZeroDims);
            }

            // Decompression-bomb guard: reject before allocating decoded pixels.
            if (info.Width > options.MaxDecodedDimensionEitherAxis || info.Height > options.MaxDecodedDimensionEitherAxis)
            {
                return Fail(HeadshotErrorCodes.DimensionsExceeded);
            }

            using var decoded = SKBitmap.Decode(codec);
            if (decoded == null || decoded.Width <= 0 || decoded.Height <= 0)
            {
                return Fail(HeadshotErrorCodes.DecodeFailed);
            }

            using var squared = CenterCropSquare(decoded);
            if (squared == null)
            {
                return Fail(HeadshotErrorCodes.CropFailed);
            }

            var targetSize = Math.Min(options.TargetDimension, squared.Width);
            if (targetSize <= 0)
            {
                return Fail(HeadshotErrorCodes.TargetZero);
            }

            using var finalBitmap = squared.Width == targetSize
                ? squared.Copy()
                : squared.Resize(new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul), SKFilterQuality.High);
            if (finalBitmap == null)
            {
                return Fail(HeadshotErrorCodes.ResizeFailed);
            }

            using var image = SKImage.FromBitmap(finalBitmap);
            if (image == null)
            {
                return Fail(HeadshotErrorCodes.ImageFailed);
            }

            using var data = image.Encode(SKEncodedImageFormat.Png, PngEncodeQuality);
            if (data == null)
            {
                return Fail(HeadshotErrorCodes.EncodeFailed);
            }

            var encoded = data.ToArray();
            if (encoded.Length > options.MaxOutputBytes)
            {
                return Fail(HeadshotErrorCodes.OutputTooLarge);
            }

            var hash = ComputeSha256Hex(encoded);
            return new NormalizeResult(true, null, encoded, hash, finalBitmap.Width, finalBitmap.Height);
        }
        catch (Exception)
        {
            return Fail(HeadshotErrorCodes.Exception);
        }
    }

    public static bool IsPng(byte[] bytes)
    {
        if (bytes == null || bytes.Length < PngMagic.Length)
        {
            return false;
        }

        for (var i = 0; i < PngMagic.Length; i++)
        {
            if (bytes[i] != PngMagic[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsJpeg(byte[] bytes)
    {
        if (bytes == null || bytes.Length < JpegMagicPrefix.Length)
        {
            return false;
        }

        for (var i = 0; i < JpegMagicPrefix.Length; i++)
        {
            if (bytes[i] != JpegMagicPrefix[i])
            {
                return false;
            }
        }

        return true;
    }

    public static string ComputeSha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Translate a <see cref="NormalizeResult.ErrorCode"/> into a localized user-facing message.
    /// Used by both the client and server to keep error messaging consistent.
    /// </summary>
    public static string GetErrorMessage(string errorCode, int maxKb)
    {
        return errorCode switch
        {
            HeadshotErrorCodes.Empty => Lang.Get("thebasics:headshot-error-empty"),
            HeadshotErrorCodes.UnsupportedFormat => Lang.Get("thebasics:headshot-error-format"),
            HeadshotErrorCodes.DimensionsExceeded => Lang.Get("thebasics:headshot-error-dimensions"),
            HeadshotErrorCodes.OutputTooLarge => Lang.Get("thebasics:headshot-error-too-large", maxKb),
            HeadshotErrorCodes.DecodeFailed
                or HeadshotErrorCodes.DecodeCreateFailed
                or HeadshotErrorCodes.DecodeZeroDims => Lang.Get("thebasics:headshot-error-decode"),
            _ => Lang.Get("thebasics:headshot-error-generic")
        };
    }

    private static SKBitmap CenterCropSquare(SKBitmap source)
    {
        var size = Math.Min(source.Width, source.Height);
        if (size <= 0)
        {
            return null;
        }

        if (source.Width == source.Height)
        {
            return source.Copy();
        }

        var x = (source.Width - size) / 2;
        var y = (source.Height - size) / 2;
        var dest = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            using var canvas = new SKCanvas(dest);
            canvas.DrawBitmap(source, new SKRect(x, y, x + size, y + size), new SKRect(0, 0, size, size));
            return dest;
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }

    private static NormalizeResult Fail(string code)
    {
        return new NormalizeResult(false, code, null, null, 0, 0);
    }

    /// <summary>
    /// Build a HeadshotMetadata from a successful Normalize result.
    /// </summary>
    public static HeadshotMetadata BuildMetadata(NormalizeResult result, long unixMs)
    {
        if (result == null || !result.Ok)
        {
            return null;
        }

        return new HeadshotMetadata
        {
            Hash = result.Hash,
            UpdatedAtUnixMs = unixMs,
            Width = result.Width,
            Height = result.Height,
            ByteLength = result.PngBytes?.Length ?? 0
        };
    }
}
