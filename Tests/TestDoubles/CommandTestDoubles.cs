using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Tests.TestDoubles;

public class TestCommandCaller : ICommandCaller
{
    public IServerPlayer Player { get; set; }
    public int FromChatGroupId { get; set; }
    public bool IsPrivileged(string privilege) => true;
    public void SendMessage(string message, EnumChatType chatType = EnumChatType.CommandSuccess) { }
}

public class TestCommandParser : ICommandArgumentParser
{
    public bool IsMissing { get; set; }
    public object Value { get; set; }
    public object GetValue() => Value;
}

public class TestCommandCallingArgs : TextCommandCallingArgs
{
    public TestCommandCallingArgs()
    {
        Caller = new TestCommandCaller();
        Parsers = new List<ICommandArgumentParser>();
    }

    public override ICommandCaller Caller { get; set; }
    public override List<ICommandArgumentParser> Parsers { get; set; }
} 