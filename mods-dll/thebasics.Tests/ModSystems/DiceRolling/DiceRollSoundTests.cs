using thebasics.Extensions;
using thebasics.Tests.Support;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class DiceRollSoundTests
{
    [Fact]
    public void DiceMuteIsPerListenerAndDefaultsOn()
    {
        var muted = new FakeServerPlayer("muted");
        var other = new FakeServerPlayer("other");
        Assert.True(muted.GetDiceRollSoundsEnabled());
        muted.SetDiceRollSoundsEnabled(false);
        Assert.False(muted.GetDiceRollSoundsEnabled());
        Assert.True(other.GetDiceRollSoundsEnabled());
        muted.SetDiceRollSoundsEnabled(true);
        Assert.True(muted.GetDiceRollSoundsEnabled());
    }

    [Fact]
    public void MalformedStoredPreferenceDefaultsOn()
    {
        var player = new FakeServerPlayer();
        player.SetModdata("thebasics-dice-roll-sounds-enabled", []);
        Assert.True(player.GetDiceRollSoundsEnabled());
    }
}
