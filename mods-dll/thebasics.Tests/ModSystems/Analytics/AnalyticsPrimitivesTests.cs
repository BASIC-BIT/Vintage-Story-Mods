using FluentAssertions;
using thebasics.ModSystems.Analytics;

namespace thebasics.Tests.ModSystems.Analytics;

public class AnalyticsPrimitivesTests
{
    [Theory]
    [InlineData(-1, "0")]
    [InlineData(0, "0")]
    [InlineData(1, "1-5")]
    [InlineData(5, "1-5")]
    [InlineData(6, "6-10")]
    [InlineData(20, "11-20")]
    [InlineData(21, "21-50")]
    [InlineData(101, "101+")]
    public void CountReturnsStableBuckets(int count, string expected)
    {
        AnalyticsBuckets.Count(count).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "<1m")]
    [InlineData(1, "1-5m")]
    [InlineData(5, "5-30m")]
    [InlineData(30, "30-120m")]
    [InlineData(120, "120m+")]
    public void DurationReturnsStableBuckets(double minutes, string expected)
    {
        AnalyticsBuckets.Duration(TimeSpan.FromMinutes(minutes)).Should().Be(expected);
    }

    [Fact]
    public void NewHexIdReturnsLowercaseHexOfRequestedSize()
    {
        var id = AnalyticsPseudonymizer.NewHexId(16);

        id.Should().HaveLength(32);
        id.Should().MatchRegex("^[a-f0-9]{32}$");
        AnalyticsPseudonymizer.IsHexString(id, 32).Should().BeTrue();
    }

    [Fact]
    public void PlayerPseudonymIsStableAndServerSaltScoped()
    {
        var saltA = new string('a', AnalyticsPseudonymizer.PlayerPseudonymSaltHexLength);
        var saltB = new string('b', AnalyticsPseudonymizer.PlayerPseudonymSaltHexLength);

        var first = AnalyticsPseudonymizer.CreatePlayerPseudonym(saltA, "player-uid-1");
        var second = AnalyticsPseudonymizer.CreatePlayerPseudonym(saltA, "player-uid-1");
        var differentSalt = AnalyticsPseudonymizer.CreatePlayerPseudonym(saltB, "player-uid-1");
        var differentPlayer = AnalyticsPseudonymizer.CreatePlayerPseudonym(saltA, "player-uid-2");

        first.Should().Be(second);
        first.Should().HaveLength(AnalyticsPseudonymizer.PlayerPseudonymHexLength);
        first.Should().MatchRegex("^[a-f0-9]{64}$");
        differentSalt.Should().NotBe(first);
        differentPlayer.Should().NotBe(first);
    }

    [Fact]
    public void PlayerPseudonymRejectsInvalidInputs()
    {
        AnalyticsPseudonymizer.CreatePlayerPseudonym("not-hex", "player-uid").Should().BeNull();
        AnalyticsPseudonymizer.CreatePlayerPseudonym(new string('a', 64), "").Should().BeNull();
    }
}
