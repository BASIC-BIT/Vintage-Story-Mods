using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ChatDisplayClearTests
{
    [Fact]
    public void ClearsOnlyTheSelectedBufferAndUndoesWithoutChangingOrder()
    {
        var chat = new ChatDisplayClear();
        var local = new LimitedList<string>(4, ["first", "second"]);
        var general = new LimitedList<string>(4, ["general"]);

        chat.Clear(local);
        Assert.Empty(chat.Visible(local));
        Assert.Equal(["general"], chat.Visible(general));
        chat.BeforeAppend(local);
        local.Add("new");
        Assert.Equal(["new"], chat.Visible(local));
        chat.Undo(local);
        Assert.Equal(["first", "second", "new"], chat.Visible(local));
    }

    [Fact]
    public void NormalBufferEvictionStillAppliesAndResetUnhidesRemainingLines()
    {
        var chat = new ChatDisplayClear();
        var buffer = new LimitedList<string>(2, ["same", "same"]);

        chat.Clear(buffer);
        chat.BeforeAppend(buffer);
        buffer.Add("same");
        Assert.Single(chat.Visible(buffer));
        chat.BeforeAppend(buffer);
        buffer.Add("latest");
        Assert.Equal(["same", "latest"], chat.Visible(buffer));
        chat.Clear(buffer);
        chat.Reset();
        Assert.Equal(["same", "latest"], chat.Visible(buffer));
    }
}
