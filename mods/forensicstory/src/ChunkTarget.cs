using NLog;
using NLog.Targets;

namespace forensicstory.src
{
    public class ChunkTarget : Target
    {
        protected virtual void Write(LogEventInfo logEvent)
        {
        }
    }
}