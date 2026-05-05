using FluentAssertions;
using thebasics.ModSystems.Repair;

namespace thebasics.Tests.ModSystems.Repair;

public class RepairModSystemTests
{
    [Theory]
    [InlineData("1", 450, 1, false)]
    [InlineData("99999", 450, 450, false)]
    [InlineData("50%", 450, 225, false)]
    [InlineData("12.5%", 400, 50, false)]
    [InlineData("100", 450, 100, true)]
    public void TryParseDurabilityInput_AcceptsAbsoluteAndPercentValues(
        string input,
        int maxDurability,
        int expectedDurability,
        bool expectedHint)
    {
        var result = RepairModSystem.TryParseDurabilityInput(
            input,
            maxDurability,
            out var durability,
            out var error,
            out var showPercentHint);

        result.Should().BeTrue();
        durability.Should().Be(expectedDurability);
        error.Should().Be(RepairModSystem.DurabilityInputError.None);
        showPercentHint.Should().Be(expectedHint);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-10%")]
    public void TryParseDurabilityInput_RejectsNegativeValues(string input)
    {
        var result = RepairModSystem.TryParseDurabilityInput(
            input,
            450,
            out _,
            out var error,
            out _);

        result.Should().BeFalse();
        error.Should().Be(RepairModSystem.DurabilityInputError.Negative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("50 percent")]
    [InlineData("NaN%")]
    [InlineData("Infinity%")]
    [InlineData("-Infinity%")]
    public void TryParseDurabilityInput_RejectsInvalidValues(string input)
    {
        var result = RepairModSystem.TryParseDurabilityInput(
            input,
            450,
            out _,
            out var error,
            out _);

        result.Should().BeFalse();
        error.Should().Be(RepairModSystem.DurabilityInputError.Invalid);
    }
}
