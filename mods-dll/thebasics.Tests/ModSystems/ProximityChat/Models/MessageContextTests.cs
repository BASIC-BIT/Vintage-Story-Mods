using FluentAssertions;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.ProximityChat.Models;

public class MessageContextTests
{
    private MessageContext CreateContext(string message = "test")
    {
        return new MessageContext
        {
            Message = message,
            GroupId = 1
        };
    }

    public class FlagTests : MessageContextTests
    {
        [Fact]
        public void HasFlag_ReturnsFalse_WhenNotSet()
        {
            var ctx = CreateContext();
            ctx.HasFlag("someFlag").Should().BeFalse();
        }

        [Fact]
        public void SetFlag_ThenHasFlag_ReturnsTrue()
        {
            var ctx = CreateContext();
            ctx.SetFlag("myFlag");
            ctx.HasFlag("myFlag").Should().BeTrue();
        }

        [Fact]
        public void SetFlag_WithFalse_ThenHasFlag_ReturnsFalse()
        {
            var ctx = CreateContext();
            ctx.SetFlag("myFlag", true);
            ctx.SetFlag("myFlag", false);
            ctx.HasFlag("myFlag").Should().BeFalse();
        }

        [Fact]
        public void SetFlag_DefaultsToTrue()
        {
            var ctx = CreateContext();
            ctx.SetFlag("myFlag");
            ctx.Flags["myFlag"].Should().BeTrue();
        }
    }

    public class MetadataTests : MessageContextTests
    {
        [Fact]
        public void SetAndGetMetadata_RoundTrips()
        {
            var ctx = CreateContext();
            ctx.SetMetadata("key", 42);
            ctx.GetMetadata<int>("key").Should().Be(42);
        }

        [Fact]
        public void GetMetadata_WithDefault_ReturnsDefault_WhenMissing()
        {
            var ctx = CreateContext();
            ctx.GetMetadata("missing", "fallback").Should().Be("fallback");
        }

        [Fact]
        public void GetMetadata_WithDefault_ReturnsValue_WhenPresent()
        {
            var ctx = CreateContext();
            ctx.SetMetadata("key", "actual");
            ctx.GetMetadata("key", "fallback").Should().Be("actual");
        }

        [Fact]
        public void HasMetadata_ReturnsFalse_WhenMissing()
        {
            var ctx = CreateContext();
            ctx.HasMetadata("nope").Should().BeFalse();
        }

        [Fact]
        public void HasMetadata_ReturnsTrue_WhenPresent()
        {
            var ctx = CreateContext();
            ctx.SetMetadata("key", "value");
            ctx.HasMetadata("key").Should().BeTrue();
        }

        [Fact]
        public void TryGetMetadata_ReturnsFalse_WhenMissing()
        {
            var ctx = CreateContext();
            ctx.TryGetMetadata<string>("missing", out var value).Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void TryGetMetadata_ReturnsTrue_WhenPresent()
        {
            var ctx = CreateContext();
            ctx.SetMetadata("key", "hello");
            ctx.TryGetMetadata<string>("key", out var value).Should().BeTrue();
            value.Should().Be("hello");
        }

        [Fact]
        public void TryGetMetadata_ReturnsFalse_WhenTypeMismatch()
        {
            var ctx = CreateContext();
            ctx.SetMetadata("key", 42);
            ctx.TryGetMetadata<string>("key", out var value).Should().BeFalse();
            value.Should().BeNull();
        }
    }

    public class UpdateMessageTests : MessageContextTests
    {
        [Fact]
        public void UpdateMessage_SetsMessage()
        {
            var ctx = CreateContext("original");
            ctx.UpdateMessage("updated");
            ctx.Message.Should().Be("updated");
        }

        [Fact]
        public void UpdateMessage_SetsSpeechText_WhenIsSpeech()
        {
            var ctx = CreateContext("original");
            ctx.SetFlag(MessageContext.IS_SPEECH);
            ctx.UpdateMessage("updated");
            ctx.TryGetSpeechText(out var speech).Should().BeTrue();
            speech.Should().Be("updated");
        }

        [Fact]
        public void UpdateMessage_DoesNotSetSpeechText_WhenNotSpeech()
        {
            var ctx = CreateContext("original");
            ctx.UpdateMessage("updated");
            ctx.TryGetSpeechText(out _).Should().BeFalse();
        }

        [Fact]
        public void UpdateMessage_SkipsSpeechUpdate_WhenFalseParam()
        {
            var ctx = CreateContext("original");
            ctx.SetFlag(MessageContext.IS_SPEECH);
            ctx.UpdateMessage("updated", updateSpeech: false);
            ctx.TryGetSpeechText(out _).Should().BeFalse();
        }
    }

    public class SpeechTextTests : MessageContextTests
    {
        [Fact]
        public void SetAndTryGetSpeechText_RoundTrips()
        {
            var ctx = CreateContext();
            ctx.SetSpeechText("hello");
            ctx.TryGetSpeechText(out var text).Should().BeTrue();
            text.Should().Be("hello");
        }

        [Fact]
        public void SetSpeechText_Null_RemovesIt()
        {
            var ctx = CreateContext();
            ctx.SetSpeechText("hello");
            ctx.SetSpeechText(null);
            ctx.TryGetSpeechText(out _).Should().BeFalse();
        }
    }

    public class DefaultStateTests : MessageContextTests
    {
        [Fact]
        public void State_DefaultsToContinue()
        {
            var ctx = CreateContext();
            ctx.State.Should().Be(MessageContextState.CONTINUE);
        }

        [Fact]
        public void Metadata_DefaultsToEmpty()
        {
            var ctx = CreateContext();
            ctx.Metadata.Should().BeEmpty();
        }

        [Fact]
        public void Flags_DefaultsToEmpty()
        {
            var ctx = CreateContext();
            ctx.Flags.Should().BeEmpty();
        }
    }
}
