using FluentAssertions;
using thebasics.Extensions;

namespace thebasics.Tests.Extensions;

public class ArrayExtensionsTests
{
    [Fact]
    public void GetRandomElement_SingleElement_ReturnsThatElement()
    {
        var array = new[] { "only" };
        array.GetRandomElement().Should().Be("only");
    }

    [Fact]
    public void GetRandomElement_ReturnsElementFromArray()
    {
        var array = new[] { "a", "b", "c" };
        var result = array.GetRandomElement();
        array.Should().Contain(result);
    }

    [Fact]
    public void GetRandomElement_EmptyArray_ThrowsArgumentException()
    {
        var array = Array.Empty<string>();
        var act = () => array.GetRandomElement();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetRandomElement_WithCustomRandom_SingleElement_ReturnsThatElement()
    {
        var array = new[] { 42 };
        array.GetRandomElement(new Random(0)).Should().Be(42);
    }

    [Fact]
    public void GetRandomElement_WithCustomRandom_ReturnsElementFromArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var result = array.GetRandomElement(new Random(42));
        array.Should().Contain(result);
    }

    [Fact]
    public void GetRandomElement_WithCustomRandom_EmptyArray_ThrowsArgumentException()
    {
        var array = Array.Empty<int>();
        var act = () => array.GetRandomElement(new Random(0));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetRandomElement_WithSameRandom_IsDeterministic()
    {
        var array = new[] { "a", "b", "c", "d", "e" };
        var r1 = array.GetRandomElement(new Random(42));
        var r2 = array.GetRandomElement(new Random(42));
        r1.Should().Be(r2);
    }
}
