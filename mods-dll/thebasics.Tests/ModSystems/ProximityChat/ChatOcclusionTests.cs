using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
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
            config.RequireLineOfSightForSpeech.Values.Should().AllSatisfy(required => required.Should().BeFalse());
        }

        [Fact]
        public void LineOfSightIsConfigurablePerMode()
        {
            var config = CreateConfig();

            config.RequireLineOfSightForSpeech.Keys.Should().BeEquivalentTo(
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
            var player = Substitute.For<IServerPlayer>();
            player.PlayerUID.Returns(uid);
            return player;
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
        public void SurvivesRoundTripThroughRangeConfig()
        {
            var config = CreateConfig();
            config.ProximityChatModeDistances[ProximityChatMode.Normal] = ModConfig.UnlimitedRange;

            ModConfig.IsUnlimitedRange(config.ProximityChatModeDistances[ProximityChatMode.Normal])
                .Should().BeTrue();
        }
    }
}
