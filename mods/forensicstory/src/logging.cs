using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace forensicstory.src
{

    //Registers Logging as a valid BlockBehavior so it can be applied to blocks
    public class LoggingSystem : ModSystem
    {
        private ICoreServerAPI _api;
        public override void StartServerSide(ICoreServerAPI api)
        {
            this._api = api;
            base.StartServerSide(api);
            //api.RegisterBlockBehaviorClass("Logging", typeof(Logging));
            api.Event.DidUseBlock += E_DidUseBlock;
        }

        private void E_DidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            String ingameDate = _api.World.Calendar.PrettyDate();
            String playerID = byPlayer.PlayerUID;
            String playerName = byPlayer.PlayerName;
            int blockX = (blockSel.Position.X - 512000);
            int blockY = blockSel.Position.Y;
            int blockZ = (blockSel.Position.Z - 512000);
            IBlockAccessor block = _api.World.GetBlockAccessor(false, false, false, false);
            String blockName = block.GetBlock(blockSel.Position).GetPlacedBlockName(_api.World, blockSel.Position);

            LogToText(playerName, playerID, blockX, blockY, blockZ, blockName, ingameDate);
        }

        public bool LogToText(String username, String userID, int x, int y, int z, string blockName, string date)
        {
            try
            {
                using (StreamWriter w = File.AppendText("BlockAccessLogs.txt"))
                {
                    String output = username + " | " + userID + " | Position:" + x + "," + y + "," + z + " | " + blockName + " | Ingame date: " + date + " | " + System.DateTime.Now;
                    w.WriteLine(output);
                    //Console.WriteLine(output);
                    return true;

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Source);
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}