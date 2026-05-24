using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Per-process LRU cache mapping headshot hash → loaded texture so that revisiting a player's
    /// bio doesn't re-fetch and re-decode bytes already in VRAM. The texture is owned by the cache
/// and disposed when evicted or on shutdown.
/// </summary>
public sealed class HeadshotClientCache
{
    private const int DefaultCapacity = 32;

    private readonly int _capacity;
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, Entry> _entries = new();

    private sealed class Entry
    {
        public LoadedTexture Texture;
        public byte[] PngBytes;
        public int Width;
        public int Height;
        public LinkedListNode<string> LruNode;
    }

    public HeadshotClientCache(ICoreClientAPI capi, int capacity = DefaultCapacity)
    {
        _ = capi ?? throw new ArgumentNullException(nameof(capi));
        _capacity = capacity > 0 ? capacity : DefaultCapacity;
    }

    public bool TryGet(string hash, out LoadedTexture texture, out int width, out int height)
    {
        texture = null;
        width = 0;
        height = 0;
        if (string.IsNullOrEmpty(hash) || !_entries.TryGetValue(hash, out var entry))
        {
            return false;
        }

        Touch(entry);
        texture = entry.Texture;
        width = entry.Width;
        height = entry.Height;
        return texture != null && texture.TextureId != 0;
    }

    /// <summary>
    /// Returns the original PNG bytes for the cached headshot. Used when we need pixel-level
    /// access (e.g. compositing into a Cairo surface for the nametag texture). Empty when the
    /// hash is unknown or bytes weren't cached.
    /// </summary>
    public byte[] TryGetPngBytes(string hash)
    {
        if (string.IsNullOrEmpty(hash) || !_entries.TryGetValue(hash, out var entry))
        {
            return Array.Empty<byte>();
        }

        Touch(entry);
        return entry.PngBytes;
    }

    public void Put(string hash, LoadedTexture texture, byte[] pngBytes, int width, int height)
    {
        if (string.IsNullOrEmpty(hash) || texture == null)
        {
            return;
        }

        if (_entries.TryGetValue(hash, out var existing))
        {
            existing.Texture?.Dispose();
            existing.Texture = texture;
            existing.PngBytes = pngBytes;
            existing.Width = width;
            existing.Height = height;
            Touch(existing);
            return;
        }

        var entry = new Entry { Texture = texture, PngBytes = pngBytes, Width = width, Height = height };
        entry.LruNode = _lru.AddFirst(hash);
        _entries[hash] = entry;

        EvictIfNeeded();
    }

    public void Invalidate(string hash)
    {
        if (string.IsNullOrEmpty(hash) || !_entries.TryGetValue(hash, out var entry))
        {
            return;
        }

        entry.Texture?.Dispose();
        _lru.Remove(entry.LruNode);
        _entries.Remove(hash);
    }

    public void Clear()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Texture?.Dispose();
        }

        _entries.Clear();
        _lru.Clear();
    }

    private void Touch(Entry entry)
    {
        _lru.Remove(entry.LruNode);
        _lru.AddFirst(entry.LruNode);
    }

    private void EvictIfNeeded()
    {
        while (_entries.Count > _capacity && _lru.Last is { } oldest)
        {
            _lru.RemoveLast();
            if (_entries.TryGetValue(oldest.Value, out var entry))
            {
                entry.Texture?.Dispose();
                _entries.Remove(oldest.Value);
            }
        }
    }
}
