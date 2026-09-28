using FluentAssertions;
using thebasics.Gui;

namespace thebasics.Tests.Gui;

public class TextAreaScrollTests
{
    [Fact]
    public void ShortTextKeepsTheVisibleHeight()
    {
        TextAreaScroll.ContentHeight(3, 24, 1, 260).Should().Be(260);
    }

    [Fact]
    public void LongTextGrowsByLineHeightInUnscaledUnits()
    {
        TextAreaScroll.ContentHeight(20, 24, 1, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
        TextAreaScroll.ContentHeight(20, 48, 2, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
    }

    [Fact]
    public void ZeroGuiScaleIsTreatedAsOne()
    {
        TextAreaScroll.ContentHeight(20, 24, 0, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
    }

    [Theory]
    [InlineData(50, 74, 0, 100, 400, 0)]      // caret already visible: stay put
    [InlineData(120, 144, 100, 100, 400, 100)] // caret inside the window: stay put
    [InlineData(20, 44, 100, 100, 400, 20)]   // caret above the window: scroll up to it
    [InlineData(230, 254, 100, 100, 400, 154)] // caret below the window: scroll down to show it
    [InlineData(390, 414, 100, 100, 400, 300)] // never past the end
    [InlineData(-10, 14, 50, 100, 400, 0)]    // never above the start
    [InlineData(230, 254, 0, 100, 80, 0)]     // content shorter than the window: no scroll
    public void TargetKeepsTheCaretLineInView(double top, double bottom, double current, double visible, double total, double expected)
    {
        TextAreaScroll.TargetFor(top, bottom, current, visible, total).Should().Be(expected);
    }
}
