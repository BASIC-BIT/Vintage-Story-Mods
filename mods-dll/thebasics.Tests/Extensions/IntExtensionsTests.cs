using FluentAssertions;
using thebasics.Extensions;

namespace thebasics.Tests.Extensions;

public class IntExtensionsTests
{
    [Fact]
    public void DoTimes_WithIndex_ReturnsCorrectCount()
    {
        var result = 5.DoTimes(i => i).ToList();
        result.Should().HaveCount(5);
    }

    [Fact]
    public void DoTimes_WithIndex_PassesCorrectIndices()
    {
        var result = 3.DoTimes(i => i).ToList();
        result.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void DoTimes_WithoutIndex_ReturnsCorrectCount()
    {
        var result = 4.DoTimes(() => "x").ToList();
        result.Should().HaveCount(4);
        result.Should().AllBe("x");
    }

    [Fact]
    public void DoTimes_Zero_ReturnsEmpty()
    {
        var result = 0.DoTimes(i => i).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void DoTimes_CallsMethodCorrectNumberOfTimes()
    {
        int callCount = 0;
        var result = 3.DoTimes(() =>
        {
            callCount++;
            return callCount;
        }).ToList();

        // Must materialize the enumerable to trigger calls
        callCount.Should().Be(3);
        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }
}
