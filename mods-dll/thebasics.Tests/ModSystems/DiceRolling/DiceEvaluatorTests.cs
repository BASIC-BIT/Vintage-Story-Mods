using thebasics.ModSystems.DiceRolling;
namespace thebasics.Tests.ModSystems.DiceRolling;

public partial class DiceEvaluatorTests
{
    [Fact]
    public void Ordinary_roll_preserves_faces_and_adds_constant()
    {
        var faces = new Queue<int>(new[] { 4, 5 });
        var result = DiceEvaluator.EvaluateInput("2d6+3", sides => { Assert.Equal(6, sides); return faces.Dequeue(); });
        Assert.Equal(12m, result.Value);
        Assert.Empty(faces);
        Assert.Contains("4", result.Breakdown);
        Assert.Contains("5", result.Breakdown);
    }
}

public partial class DiceEvaluatorTests
{
    [Theory]
    [InlineData("2d6+3*2", 15)]
    [InlineData("(2d6+3)*2", 24)]
    [InlineData("5/2", 2.5)]
    [InlineData("-(d6+2)", -6)]
    [InlineData("round(-2.5)+ceil(1.2)+floor(1.9)", 0)]
    public void Arithmetic_and_helpers_follow_mathematical_rules(string input, double expected)
    {
        var faces = new Queue<int>(new[] { 4, 5 });
        Assert.Equal((decimal)expected, DiceEvaluator.EvaluateInput(input, _ => faces.Dequeue()).Value);
    }
}

public partial class DiceEvaluatorTests
{
    [Theory]
    [InlineData("2d20kh1+3", "7,16", 19, false)]
    [InlineData("2d20kl1+3", "7,16", 10, false)]
    [InlineData("4d6dl1", "1,4,5,6", 15, false)]
    [InlineData("4d6dh1", "1,4,5,6", 10, false)]
    [InlineData("2d20kh1", "16,16", 16, false)]
    [InlineData("3d6!kh2>=5", "6,2,5,6,1", 2, true)]
    [InlineData("3d6>=5kh2!", "6,2,5,6,1", 2, true)]
    [InlineData("d6!r=1", "1,6", 6, false)]
    [InlineData("2d6ro=1", "1,1,4,5", 9, false)]
    [InlineData("2d6r=1", "1,1,4,5", 9, false)]
    [InlineData("d6rr<=2", "1,2,4", 4, false)]
    [InlineData("4d6>=5", "4,5,1,6", 2, true)]
    [InlineData("4d6>5", "4,5,1,6", 1, true)]
    [InlineData("4d6>=5 + 2", "4,5,1,6", 4, true)]
    [InlineData("2d6>=5 + 2d6", "4,5,1,6", 8, false)]
    public void Modifiers_apply_to_faces_in_fixed_categories(string input, string supplied, int expected, bool success)
    {
        var faces = new Queue<int>(supplied.Split(',').Select(int.Parse));
        var result = DiceEvaluator.EvaluateInput(input, _ => faces.Dequeue());
        Assert.Equal(expected, result.Value);
        Assert.Equal(success, result.IsSuccessPool);
        Assert.Empty(faces);
    }
}

public partial class DiceEvaluatorTests
{
    [Theory]
    [InlineData("d20 climbing", "d20", "climbing")]
    [InlineData("2d6+3 forcing the gate", "2d6+3", "forcing the gate")]
    [InlineData("2d6 + 3 # forcing 2d20", "2d6 + 3", "forcing 2d20")]
    [InlineData("2d6+3 // + strength", "2d6+3", "+ strength")]
    [InlineData("2 defend", "2", "defend")]
    [InlineData("floor( 2d6 / 3 ) reason", "floor( 2d6 / 3 )", "reason")]
    public void Reasons_have_explicit_or_unambiguous_boundaries(string input, string expression, string reason)
    {
        var result = DiceEvaluator.EvaluateInput(input, _ => 4);
        Assert.Equal(expression, result.Expression);
        Assert.Equal(reason, result.Reason);
    }

    [Theory]
    [InlineData("d20climbing")]
    [InlineData("d20 d6")]
    [InlineData("d20 round(2)")]
    [InlineData("2d6garbage")]
    [InlineData("2d6 +")]
    [InlineData("2d6 + strength")]
    [InlineData("2d6 round(2)")]
    [InlineData("2d6 d8")]
    [InlineData("2d6 kh")]
    [InlineData("2.5d6")]
    [InlineData("d6.5")]
    [InlineData("0d6")]
    [InlineData("d0")]
    [InlineData("d-1")]
    [InlineData("101d6")]
    [InlineData("60d6+41d6")]
    [InlineData("d1000001")]
    [InlineData("d1!")]
    [InlineData("d6rr>=1")]
    [InlineData("d6kh2")]
    [InlineData("{1}rr=1")]
    [InlineData("abs(2)")]
    [InlineData("d6>=5f1")]
    public void Malformed_or_statically_impossible_requests_draw_nothing(string input)
    {
        int draws = 0;
        Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput(input, _ => { draws++; return 1; }));
        Assert.Equal(0, draws);
    }

    [Theory]
    [InlineData("d6!", 6)]
    [InlineData("d6rr=1", 1)]
    public void Generated_dice_budget_errors_before_callback_101(string input, int face)
    {
        int draws = 0;
        var error = Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput(input, _ => { draws++; return face; }));
        Assert.Equal("limit", error.Code);
        Assert.Equal(100, draws);
    }

    [Fact]
    public void Parser_and_numeric_guards_return_safe_errors()
    {
        foreach (var input in new[] { new string('(', 40) + "1" + new string(')', 40), new string('1', 513), string.Join('+', Enumerable.Repeat("1", 140)) })
            Assert.Equal("limit", Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput(input)).Code);
        Assert.Equal("division_by_zero", Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput("1/(1-1)")).Code);
        Assert.Equal("overflow", Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput("79228162514264337593543950335*2")).Code);
        Assert.Equal("invalid_face", Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput("d6", _ => 7)).Code);
    }

    [Fact]
    public void Breakdown_preserves_discarded_and_generated_faces()
    {
        var faces = new Queue<int>(new[] { 1, 6, 2, 4 });
        var result = DiceEvaluator.EvaluateInput("2d6!r=1kh1", _ => faces.Dequeue());
        Assert.Equal(6m, result.Value);
        Assert.Contains("1 replaced", result.Breakdown);
        Assert.Contains("6 kept", result.Breakdown);
        Assert.Contains("2 generated dropped", result.Breakdown);
        Assert.Contains("4 generated dropped", result.Breakdown);
        Assert.Empty(faces);
    }

    [Theory]
    [InlineData("d20", 20)]
    [InlineData("d20 # reason", 20)]
    [InlineData("1d20", null)]
    [InlineData("(d20)", null)]
    [InlineData("d20+1", null)]
    public void Only_plain_dN_has_numbered_bubble_icon(string input, int? sides) => Assert.Equal(sides, DiceEvaluator.EvaluateInput(input, _ => 1).SimpleSides);
}

public partial class DiceEvaluatorTests
{
    [Theory]
    [InlineData("1.000000000000000000000000000001d6")]
    [InlineData("d6.000000000000000000000000000001")]
    [InlineData("2d6 D20")]
    [InlineData("2d6 d.5")]
    [InlineData("2d6 d")]
    public void Ambiguity_and_precision_loss_cannot_become_a_successful_roll(string input)
    {
        int draws = 0;
        Assert.Throws<DiceRollException>(() => DiceEvaluator.EvaluateInput(input, _ => { draws++; return 1; }));
        Assert.Equal(0, draws);
    }
}
