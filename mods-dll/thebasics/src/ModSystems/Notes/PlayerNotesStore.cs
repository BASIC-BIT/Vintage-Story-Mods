using System;
using System.IO;
using Newtonsoft.Json;
using thebasics.ModSystems.Notes.Models;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Notes;

public sealed class PlayerNotesStore
{
    private readonly string _path;
    private readonly Action<string> _warning;

    public PlayerNotesStore(ICoreServerAPI api)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));

        var savegameId = Sanitize(api.WorldManager?.SaveGame?.SavegameIdentifier ?? "default");
        var baseDir = Path.Combine(api.GetOrCreateDataPath("ModData"), "thebasics", "notes", savegameId);
        _path = Path.Combine(baseDir, "notes.json");
        _warning = message => api.Logger.Warning("[thebasics] " + message);
    }

    public PlayerNotesStore(string path, Action<string> warning = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _warning = warning ?? (_ => { });
    }

    public PlayerNotesData Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Normalize(new PlayerNotesData());
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Normalize(new PlayerNotesData());
            }

            return Normalize(JsonConvert.DeserializeObject<PlayerNotesData>(json) ?? new PlayerNotesData());
        }
        catch (Exception ex)
        {
            _warning("Failed to load player notes store. Using empty notes. " + ex.Message);
            return Normalize(new PlayerNotesData());
        }
    }

    public bool Save(PlayerNotesData data)
    {
        try
        {
            data = Normalize(data ?? new PlayerNotesData());
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data, Formatting.Indented));
            File.Move(tmp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _warning("Failed to save player notes store. " + ex.Message);
            return false;
        }
    }

    public static PlayerNotesData Normalize(PlayerNotesData data)
    {
        data ??= new PlayerNotesData();
        data.Version = Math.Max(1, data.Version);
        data.AdminNotes ??= new();
        data.AdminLedgers ??= new();
        data.PersonalLedgers ??= new();
        data.PersonalNotes ??= new();
        data.AdminNotes.RemoveAll(note => note == null);
        data.AdminLedgers.RemoveAll(ledger => ledger == null);
        data.PersonalLedgers.RemoveAll(ledger => ledger == null);
        data.PersonalNotes.RemoveAll(note => note == null);
        return data;
    }

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "default";
        }

        var span = raw.AsSpan();
        var buffer = new char[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            buffer[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_';
        }

        var result = new string(buffer);
        return result.Length > 64 ? result.Substring(0, 64) : result;
    }
}
