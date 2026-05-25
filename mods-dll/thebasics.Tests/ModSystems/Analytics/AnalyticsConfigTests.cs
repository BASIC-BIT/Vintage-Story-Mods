using FluentAssertions;
using thebasics.Configs;

namespace thebasics.Tests.ModSystems.Analytics;

public class AnalyticsConfigTests
{
    [Theory]
    [InlineData("server", AnalyticsConsentLevels.Server)]
    [InlineData(" SERVER ", AnalyticsConsentLevels.Server)]
    [InlineData("personalized", AnalyticsConsentLevels.Personalized)]
    [InlineData("off", AnalyticsConsentLevels.Disabled)]
    [InlineData("none", AnalyticsConsentLevels.Disabled)]
    [InlineData("disabled", AnalyticsConsentLevels.Disabled)]
    [InlineData("", AnalyticsConsentLevels.Unknown)]
    [InlineData("unexpected", AnalyticsConsentLevels.Unknown)]
    public void NormalizeMapsSupportedConsentAliases(string input, string expected)
    {
        AnalyticsConsentLevels.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void RemoteAnalyticsRequiresCurrentConsentPolicy()
    {
        var config = new AnalyticsConfig
        {
            ConsentLevel = AnalyticsConsentLevels.Server,
            ConsentVersionAccepted = AnalyticsConsentLevels.CurrentConsentVersion - 1
        };

        config.AllowsRemoteAnalytics().Should().BeFalse();
        config.RequiresConsentChoice().Should().BeTrue();

        config.ConsentVersionAccepted = AnalyticsConsentLevels.CurrentConsentVersion;

        config.AllowsRemoteAnalytics().Should().BeTrue();
        config.RequiresConsentChoice().Should().BeFalse();
    }

    [Fact]
    public void DisabledConsentDoesNotRequireRepeatedPrompt()
    {
        var config = new AnalyticsConfig
        {
            ConsentLevel = AnalyticsConsentLevels.Disabled,
            ConsentVersionAccepted = 0
        };

        config.AllowsRemoteAnalytics().Should().BeFalse();
        config.RequiresConsentChoice().Should().BeFalse();
    }

    [Fact]
    public void InitializeDefaultsNormalizesEndpointAndBounds()
    {
        var config = new AnalyticsConfig
        {
            ConsentLevel = " SERVER ",
            EndpointUrl = "https://analytics.basicbit.net/v1/events/batch",
            FlushIntervalSeconds = 1,
            MaxQueuedEvents = 10,
            MaxBatchEvents = 100,
            PromptRepeatHours = 1000
        };

        config.InitializeDefaultsIfNeeded();

        config.ConsentLevel.Should().Be(AnalyticsConsentLevels.Server);
        config.EndpointUrl.Should().Be(AnalyticsConfig.DefaultEndpointUrl);
        config.FlushIntervalSeconds.Should().Be(10);
        config.MaxQueuedEvents.Should().Be(25);
        config.MaxBatchEvents.Should().Be(50);
        config.PromptRepeatHours.Should().Be(168);
    }
}
