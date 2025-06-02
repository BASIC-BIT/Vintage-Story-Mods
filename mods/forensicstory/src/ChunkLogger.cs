using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace forensicstory
{
    internal class ChunkLogger<T, TLog>
        where T : Log
    {
        private ICoreServerAPI _serverApi;
        private Dictionary<IServerChunk, List<TLog>> _chunkLogs;

        private string _name;

        public ChunkLogger(ICoreServerAPI api, string name)
        {
            _serverApi = api;
            _chunkLogs = new Dictionary<IServerChunk,List<TLog>>();

            _name = name;

            api.Event.GameWorldSave += OnSaveGameSaving;
        }

        private void OnSaveGameSaving()
        {
            foreach (KeyValuePair<IServerChunk, List<TLog>> logData in _chunkLogs)
            {
                if (logData.Value.Count == 0) continue;
                logData.Key.SetServerModdata(_name, SerializerUtil.Serialize(logData.Value));
            }
        }

        public void Log(IServerPlayer player, T data)
        {
            IServerChunk chunk = _serverApi.WorldManager.GetChunk(player.Entity.ServerPos.AsBlockPos);

            if (!_chunkLogs.ContainsKey(chunk))
            {
                _chunkLogs[chunk] = new List<TLog>();
            }
            
            // Note: This method appears incomplete - data parameter is not being used
            // The generic constraint suggests TLog should be related to T, but the relationship is unclear
        }

        private void AddChunkToDictionary(IServerChunk chunk)
        {
            byte[] data = chunk.GetServerModdata(_name);
            List<TLog> logs = data == null ? new List<TLog>() : SerializerUtil.Deserialize<List<TLog>>(data);

            _chunkLogs.Add(chunk, logs);
        }
    }
}