#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Vintagestory.API.Server;

namespace thebasicslanguageunderstanding;

internal static class NativeOnnxRuntimeResolver
{
    private static readonly object Gate = new object();
    private static bool _configured;

    public static void Configure(ICoreServerAPI api)
    {
        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(InferenceSession).Assembly, ResolveOnnxRuntime);
                _configured = true;
            }
            catch (InvalidOperationException)
            {
                _configured = true;
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[thebasics-language-understanding] Failed to configure ONNX Runtime native resolver: {ex.Message}");
            }
        }
    }

    private static IntPtr ResolveOnnxRuntime(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in GetNativeLibraryCandidates(assembly))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static string[] GetNativeLibraryCandidates(Assembly assembly)
    {
        var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        var rid = GetRuntimeIdentifier();
        var fileName = GetNativeLibraryFileName();

        return new[]
        {
            Path.Combine(assemblyDir, "native", fileName),
            Path.Combine(AppContext.BaseDirectory, "native", fileName),
            Path.Combine(assemblyDir, "runtimes", rid, "native", fileName),
            Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName),
            Path.Combine(assemblyDir, fileName),
            Path.Combine(AppContext.BaseDirectory, fileName)
        };
    }

    private static string GetRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"win-{architecture}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"linux-{architecture}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"osx-{architecture}";
        }

        return RuntimeInformation.RuntimeIdentifier;
    }

    private static string GetNativeLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "onnxruntime.dll";
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "libonnxruntime.dylib"
            : "libonnxruntime.so";
    }
}
