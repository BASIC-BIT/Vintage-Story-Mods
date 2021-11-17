using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using forensicstory.util;
using Vintagestory.API.Common;

namespace forensicstory
{
    public class Logger<T> where T : Log
    {
        private readonly ICoreAPI _api;

        public static string FolderPrefix;
        public static string Extension = ".txt";

        public int BatchSize = 50;
        public int BatchWaitMs = 2000;

        protected Timer Timer;
        protected List<string> BatchContents = new List<string>();

        protected string FileName;

        public Logger(ICoreAPI api = null)
        {
            _api = api;
            FolderPrefix = Path.GetFullPath(Path.Combine(_api.DataBasePath, "Logs", "bigbrother-"));
        }

        public void Log(T log)
        {
            FileName = log.FileName;
            BatchContents.Add(log.FormatLog(_api));

            if (BatchContents.Count >= BatchSize)
            {
                RunLogTask();
            }
            else
            {
                ClearTimer();
                Timer = new Timer(BatchWaitMs)
                {
                    AutoReset = false,
                    Enabled = true,
                };
                Timer.Elapsed += RunLogTask;
            }
        }

        protected void ClearTimer()
        {
            if (Timer != null)
            {
                Timer.Stop();
                Timer.Close();
                Timer = null;
            }
        }
        
        protected void RunLogTask()
        {
            ClearTimer();
            if (BatchContents.Count > 0)
            {
                Task.Run(() =>
                {
                    try
                    {
                        using (StreamWriter w = File.AppendText(GetLogLocation()))
                        {
                            foreach (var log in BatchContents)
                            {
                                w.WriteLine(log);
                            }
                            
                            w.Flush();
                            BatchContents.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                });
            }
        }

        protected string GetLogLocation()
        {
            return $"{FolderPrefix}{FileName}-{DateUtility.GetDateString()}{Extension}";
        }

        protected void RunLogTask(object sender, ElapsedEventArgs eventArgs)
        {
            RunLogTask();
        }
    }
}