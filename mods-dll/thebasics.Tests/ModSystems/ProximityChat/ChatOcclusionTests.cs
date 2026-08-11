using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat;

/// <summary>
/// Covers the effective-distance side of chat occlusion: how a wall penalty feeds the obfuscation
/// gradient and font-size falloff. The raycast and segment sampling themselves need a live world
/// and are covered by manual QA.
/// </summary>
public class ChatOcclusionTests
{
    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    public class ConfigDefaults
    {
        [Fact]
        public void OcclusionIsOffByDefault()
        {
            var config = CreateConfig();

            config.SpeechOcclusionWallPenaltyBlocks.Should().Be(0);
            config.RequireClearSoundPathForSpeech.Values.Should().AllSatisfy(required => required.Should().BeFalse());
        }

        [Fact]
        public void LineOfSightIsConfigurablePerMode()
        {
            var config = CreateConfig();

            config.RequireClearSoundPathForSpeech.Keys.Should().BeEquivalentTo(
                [ProximityChatMode.Normal, ProximityChatMode.Whisper, ProximityChatMode.Yell]);
        }
    }

    public class OcclusionPenaltyMetadata
    {
        private static MessageContext ContextWithPenalties(params (string uid, int penalty)[] penalties)
        {
            var context = new MessageContext();
            var map = new Dictionary<string, int>();
            foreach (var (uid, penalty) in penalties)
            {
                map[uid] = penalty;
            }

            context.SetMetadata(MessageContext.OCCLUSION_PENALTY_BY_RECIPIENT, map);
            return context;
        }

        private static IServerPlayer PlayerWithUid(string uid)
        {
            return new FakeServerPlayer(uid);
        }

        [Fact]
        public void ReturnsZeroWhenNoPenaltiesRecorded()
        {
            new MessageContext().GetOcclusionPenalty(PlayerWithUid("a")).Should().Be(0);
        }

        [Fact]
        public void ReturnsZeroForRecipientWithoutAnEntry()
        {
            ContextWithPenalties(("a", 12)).GetOcclusionPenalty(PlayerWithUid("b")).Should().Be(0);
        }

        [Fact]
        public void ReturnsZeroForNullRecipient()
        {
            ContextWithPenalties(("a", 12)).GetOcclusionPenalty(null).Should().Be(0);
        }

        [Fact]
        public void ReturnsRecordedPenaltyForRecipient()
        {
            ContextWithPenalties(("a", 12), ("b", 3)).GetOcclusionPenalty(PlayerWithUid("b")).Should().Be(3);
        }
    }

    public class MarkupSafeObfuscation
    {
        // Language scrambling italicises text a listener cannot understand, so obfuscation can be
        // handed VTML. Neither '<' nor '>' is punctuation, so a blind per-character pass corrupted
        // the tag and the listener saw an empty line instead of a garbled one.
        // percentage 1.0 garbles every eligible character, the worst case for tag corruption.
        [Theory]
        [InlineData("<i>gibberish here</i>", "<i>********* ****</i>")]
        [InlineData("plain then <i>tagged</i> more", "***** **** <i>******</i> ****")]
        [InlineData("<font color=\"#ABCDEF\">hi</font>", "<font color=\"#ABCDEF\">**</font>")]
        [InlineData("no markup at all", "** ****** ** ***")]
        public void GarblesTextButNeverTags(string input, string expected)
        {
            ChatHelper.ObfuscateOutsideMarkup(input, 1.0, () => 0.0).Should().Be(expected);
        }

        // A bare '<' a player typed must still be garbled. Treating every '<' as markup would let
        // anyone type one and have the rest of their line delivered legibly at any distance.
        [Theory]
        [InlineData("a < b secret", "* * * ******")]
        [InlineData("a < b > secret", "* * * * ******")]
        [InlineData("2 <3 hearts", "* ** ******")]
        [InlineData("unclosed <i tag here", "******** ** *** ****")]
        public void RawAngleBracketsAreStillGarbled(string input, string expected)
        {
            ChatHelper.ObfuscateOutsideMarkup(input, 1.0, () => 0.0).Should().Be(expected);
        }

        [Fact]
        public void NestedOpenBracketDoesNotOpenATagSpan()
        {
            // "<i<b>" is not a tag; the first '<' must not swallow to the later '>'.
            ChatHelper.ObfuscateOutsideMarkup("<i<b>x", 1.0, () => 0.0).Should().Be("**<b>*");
        }

        [Fact]
        public void LeavesEverythingAloneAtZeroPercent()
        {
            const string input = "<i>hello there</i>";

            ChatHelper.ObfuscateOutsideMarkup(input, 0.0, () => 0.5).Should().Be(input);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void HandlesEmptyInput(string? input)
        {
            ChatHelper.ObfuscateOutsideMarkup(input!, 1.0, () => 0.0).Should().Be(input);
        }
    }

    public class UnlimitedRangeSentinel
    {
        [Fact]
        public void RecognisesTheSentinelAndNothingElse()
        {
            ModConfig.IsUnlimitedRange(ModConfig.UnlimitedRange).Should().BeTrue();
            ModConfig.IsUnlimitedRange(0).Should().BeFalse();
            ModConfig.IsUnlimitedRange(35).Should().BeFalse();
        }

        [Fact]
        public void OtherNegativeValuesAreNotUnlimited()
        {
            // A typo must not silently turn a proximity mode into a server-wide channel.
            ModConfig.IsUnlimitedRange(-2).Should().BeFalse();
            ModConfig.IsUnlimitedRange(-100).Should().BeFalse();
        }

        [Fact]
        public void SurvivesRoundTripThroughRangeConfig()
        {
            var config = CreateConfig();
            config.ProximityChatModeDistances[ProximityChatMode.Normal] = ModConfig.UnlimitedRange;

            ModConfig.IsUnlimitedRange(config.ProximityChatModeDistances[ProximityChatMode.Normal])
                .Should().BeTrue();
        }
    }
}
