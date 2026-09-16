using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat.Models;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class DiceReadabilityTests
{
    [Theory]
    [InlineData("20")]
    [InlineData(" 20 # luck")]
    [InlineData("20 // luck")]
    [InlineData("20 luck")]
    public void Bare_sides_roll_one_die_and_use_the_compact_badge(string input)
    {
        var draws = 0;
        var result = DiceEvaluator.EvaluateInput(input, sides => { Assert.Equal(20, sides); draws++; return 15; });
        Assert.Equal(1, draws);
        Assert.Equal(15, result.Value);
        Assert.Equal("d20", result.Expression);
        Assert.Equal(20, result.SimpleSides);
    }
    [Theory]
    [InlineData("d20", "d20 = <strong>15</strong>")]
    [InlineData("2d20", "2d20 = <strong>30</strong> [15, 15]")]
    [InlineData("2d20kh2", "2d20kh2 = <strong>30</strong> [15, 15]")]
    public void Ordinary_results_are_bold_without_redundant_kept_labels(string input, string expected)
    {
        var result = DiceEvaluator.EvaluateInput(input, _ => 15);
        Assert.Equal("Alice rolled " + expected, DicePresentation.Chat(result, "Alice", ProximityChatMode.Normal, false));
        Assert.DoesNotContain("kept", result.Breakdown);
    }

    [Fact]
    public void Arithmetic_constants_stay_constants()
    {
        var result = DiceEvaluator.EvaluateInput("20+2", _ => throw new System.Exception("No dice expected"));
        Assert.Equal(22, result.Value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000001")]
    public void Bare_sides_enforce_normal_die_limits_before_drawing(string input)
    {
        var draws = 0;
        Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput(input, _ => { draws++; return 1; }));
        Assert.Equal(0, draws);
    }
}
