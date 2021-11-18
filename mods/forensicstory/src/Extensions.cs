using System;
using System.Text;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace forensicstory
{
    public static class Extensions
    {
        public static readonly string LogSeparator = " | ";

        public static StringBuilder AddSeparator(this StringBuilder stringBuilder)
        {
            return stringBuilder.Append(LogSeparator);
        }

        public static StringBuilder AddLogSection(this StringBuilder stringBuilder, String section, String data)
        {
            stringBuilder.Append(section + ": " + data);
            stringBuilder.AddSeparator();

            return stringBuilder;
        }
        
        public static string GetPrettyString(this BlockPos blockPos)
        {
            int blockX = (blockPos.X - 512000);
            int blockY = blockPos.Y;
            int blockZ = (blockPos.Z - 512000);

            return blockX + "," + blockY + "," + blockZ;
        }
        
        public static string GetPrettyString(this SyncedEntityPos pos)
        {
            double blockX = (pos.X - 512000);
            double blockY = pos.Y;
            double blockZ = (pos.Z - 512000);

            return blockX + "," + blockY + "," + blockZ;
        }
    }
}