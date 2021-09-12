using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace forensicstory.src
{
    public class ChunkLoggingData
    {
        private IDictionary<BlockPos, List<Log>> blockData { get; }

        public ChunkLoggingData()
        {
            blockData = new Dictionary<BlockPos, List<Log>>();
        }

        private void AddLog(BlockPos pos, Log log)
        {
            if (blockData.ContainsKey(pos))
            {
                blockData[pos].Add(log);
            }
            else
            {
                blockData.Add(pos, new List<Log> { log });
            }
        }
    }
}