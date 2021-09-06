using System;
using System.IO;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace forensicstory.src
{
    public class Logger
    {
        private readonly ICoreAPI _api;

        public static string FolderPrefix = "./data/Logs/bigbrother-";
        public static string Extension = ".txt";

        public Logger(ICoreAPI api)
        {
            _api = api;
        }
        public Logger()
        {
            _api = null;
        }

        public void Log(Log log)
        {
            Task.Run(() =>
            {
                try
                {
                    using (StreamWriter w = File.AppendText($"{FolderPrefix}{log.FileName}{Extension}"))
                    {
                        w.WriteLine(log.FormatLog(_api));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Source);
                    Console.WriteLine(e.Message);
                }
            });
        }
    }
}