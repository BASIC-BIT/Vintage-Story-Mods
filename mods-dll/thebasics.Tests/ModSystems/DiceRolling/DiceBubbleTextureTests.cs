using thebasics.ModSystems.ChatUiSystem;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class DiceBubbleTextureTests
{
    [Theory]
    [InlineData("dice", true, null)]
    [InlineData("dice20", true, 20)]
    [InlineData("dice1000000", true, 1000000)]
    [InlineData("dice0", false, null)]
    [InlineData("dice1000001", false, null)]
    [InlineData("dice-20", false, null)]
    [InlineData("dice2d6", false, null)]
    [InlineData("env", false, null)]
    public void OnlyAcceptsBoundedServerBadgeMarkers(string kind, bool accepted, int? sides)
    {
        Assert.Equal(accepted, DiceBubbleTexture.TryGetSides(kind, out var actual));
        Assert.Equal(sides, actual);
    }
}
