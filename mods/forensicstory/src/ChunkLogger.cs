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

            
            _chunkLogs[chunk]
        }

        private void AddChunkToDictionary(IServerChunk chunk)
        {
            byte[] data = chunk.GetServerModdata("haunting");
            int haunting = data == null ? 0 : SerializerUtil.Deserialize<int>(data);

            _chunkLogs.Add(chunk, haunting);
        }
    }
}