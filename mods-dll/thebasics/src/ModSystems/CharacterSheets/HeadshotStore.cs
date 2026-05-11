using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.CharacterSheets;

/// <summary>
/// Server-side disk I/O for headshot bytes. One file per (player, characterId) lives under
/// the game's mod-data folder, namespaced by savegame so different worlds on the same server
/// install don't collide.
/// </summary>
public sealed class HeadshotStore
{
    private readonly ICoreServerAPI _api;
    private readonly string _baseDir;

    public HeadshotStore(ICoreServerAPI api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));

        var savegameId = Sanitize(api.WorldManager?.SaveGame?.SavegameIdentifier ?? "default");
        _baseDir = Path.Combine(api.GetOrCreateDataPath("ModData"), "thebasics", "headshots", savegameId);
        Directory.CreateDirectory(_baseDir);
    }

    public string ResolveFilePath(string playerUid, string characterId)
    {
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            throw new ArgumentException("playerUid required", nameof(playerUid));
        }

        var safeUid = Sanitize(playerUid);
        var safeChar = Sanitize(characterId ?? "default");
        return Path.Combine(_baseDir, safeUid + "_" + safeChar + ".png");
    }

    public bool TryRead(string playerUid, string characterId, out byte[] bytes)
    {
        bytes = null;
        string path;
        try
        {
            path = ResolveFilePath(playerUid, characterId);
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to resolve headshot path for {playerUid}/{characterId}: {ex.Message}");
            return false;
        }

        try
        {
            bytes = File.ReadAllBytes(path);
            return bytes.Length > 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to read headshot for {playerUid}/{characterId}: {ex.Message}");
            bytes = null;
            return false;
        }
    }

    /// <summary>
    /// Atomic write: writes to a .tmp sibling and renames over the destination.
    /// Returns false on any I/O failure (logged).
    /// </summary>
    public bool TryWrite(string playerUid, string characterId, byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
        {
            return false;
        }

        try
        {
            var path = ResolveFilePath(playerUid, characterId);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, pngBytes);
            // File.Move with overwrite is atomic on the same volume on modern .NET.
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to write headshot for {playerUid}/{characterId}: {ex.Message}");
            return false;
        }
    }

    public bool TryDelete(string playerUid, string characterId)
    {
        try
        {
            File.Delete(ResolveFilePath(playerUid, characterId));
            return true;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to delete headshot for {playerUid}/{characterId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Replace any reserved/path-injection characters with underscores. Player UIDs are
    /// already alphanumeric in practice, but we treat them as untrusted input on the
    /// off-chance a custom auth provider produces something exotic.
    /// </summary>
    private static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "default";
        }

        var span = raw.AsSpan();
        var buf = new char[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            buf[i] = (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_';
        }

        var result = new string(buf);
        return result.Length > 64 ? result.Substring(0, 64) : result;
    }
}
