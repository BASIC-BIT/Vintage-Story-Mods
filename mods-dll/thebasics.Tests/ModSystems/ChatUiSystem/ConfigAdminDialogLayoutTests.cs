using FluentAssertions;
using Vintagestory.API.Client;
using ConfigChatUiSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ConfigAdminDialogLayoutTests
{
    [Fact]
    public void LongStatusMessageReservesAdditionalWrappedTextHeight()
    {
        const string message = "Sight block pattern 'game:glass-*' appears in both sight override lists. " +
                               "Remove it from one list; blocking would otherwise take precedence.\n" +
                               "11 blocks match both sight override lists: 'game:glass-blue', 'game:glass-brown', " +
                               "'game:glass-green', 'game:glass-pink', 'game:glass-plain', 'game:glass-quartz', " +
                               "'game:glass-red', 'game:glass-smoky', 'game:glass-vintage', 'game:glass-violet' " +
                               "(1 more omitted). Adjust the patterns so each block resolves to only one list.";
        string? measuredMessage = null;
        double measuredWidth = 0;
        EnumLinebreakBehavior? measuredLinebreakBehavior = null;

        var layout = ConfigChatUiSystem.CalculateConfigAdminStatusLayout(message, (text, width, linebreakBehavior) =>
        {
            measuredMessage = text;
            measuredWidth = width;
            measuredLinebreakBehavior = linebreakBehavior;
            return 108;
        }, guiScale: 1, maximumHeight: 180);

        measuredMessage.Should().Be(message);
        measuredWidth.Should().Be(648);
        measuredLinebreakBehavior.Should().Be(EnumLinebreakBehavior.AfterWord);
        layout.Text.Should().Be(message);
        layout.Height.Should().Be(126);
    }

    [Fact]
    public void ShortStatusMessageKeepsMinimumHeight()
    {
        var layout = ConfigChatUiSystem.CalculateConfigAdminStatusLayout("Saved.", (_, _, _) => 18, guiScale: 1, maximumHeight: 180);

        layout.Text.Should().Be("Saved.");
        layout.Height.Should().Be(42);
    }

    [Fact]
    public void OversizedStatusMessageIsBoundedAndPointsToFullChatDetails()
    {
        var message = new string('x', 400);

        var layout = ConfigChatUiSystem.CalculateConfigAdminStatusLayout(message, (text, _, _) => text.Length * 2, guiScale: 1, maximumHeight: 180);

        layout.Text.Should().EndWith("\n(Full details are in chat.)");
        layout.Text.Should().NotBe(message);
        layout.Height.Should().BeLessThanOrEqualTo(180);
    }

    [Fact]
    public void AvailableFrameHeightPreservesSpaceForOtherRows()
    {
        var otherRows = new[]
        {
            new DialogRow(new DialogElement { Height = 300 }) { TopPadding = 10, BottomPadding = 8 }
        };

        var maximumHeight = ConfigChatUiSystem.CalculateConfigAdminStatusMaximumHeight(otherRows, frameHeight: 450, guiScale: 1);

        maximumHeight.Should().Be(110);
    }
}
