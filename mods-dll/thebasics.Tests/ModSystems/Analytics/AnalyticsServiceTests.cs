using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.Analytics;

[Collection("AnalyticsService static state")]
public class AnalyticsServiceTests
{
    [Fact]
    public void TrackConfigSnapshotIncludesOnlySummariesForNewerFeatureFamilies()
    {
        var sink = new CapturingAnalyticsSink();
        AnalyticsService.Configure(sink);

        try
        {
            var config = new ModConfig
            {
                EnableCharacterSheets = true,
                EnableCharacterHeadshots = true,
                ShowHeadshotInNametag = false,
                HeadshotUrlAllowed = false,
                UseCustomNametagRenderer = true,
                EnableRpCharacterSlots = true,
                MaxRpCharacterSlots = 7,
                EnableAdminNotes = true,
                EnableStructuredAdminNotes = false,
                EnableAdminNoteLedger = true,
                EnablePlayerNotes = false,
                CharacterSheetFields = Enumerable.Range(0, 12)
                    .Select(i => new CharacterSheetFieldDefinition { Id = $"field{i}" })
                    .ToList(),
                Languages = Enumerable.Range(0, 2)
                    .Select(i => new Language($"Lang{i}", string.Empty, $"l{i}", ["la"], "#ffffff"))
                    .ToList()
            };
            config.InitializeDefaultsIfNeeded();

            AnalyticsService.TrackConfigSnapshot(config);

            var captured = sink.Events.Should().ContainSingle().Subject;
            captured.Name.Should().Be("config snapshot");
            captured.Properties.Should().ContainKey("enable_character_sheets").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("enable_character_headshots").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("show_headshot_in_nametag").WhoseValue.Should().Be(false);
            captured.Properties.Should().ContainKey("headshot_url_allowed").WhoseValue.Should().Be(false);
            captured.Properties.Should().ContainKey("use_custom_nametag_renderer").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("enable_rp_character_slots").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("max_rp_character_slots_bucket").WhoseValue.Should().Be("6-10");
            captured.Properties.Should().ContainKey("enable_admin_notes").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("enable_structured_admin_notes").WhoseValue.Should().Be(false);
            captured.Properties.Should().ContainKey("enable_admin_note_ledger").WhoseValue.Should().Be(true);
            captured.Properties.Should().ContainKey("enable_player_notes").WhoseValue.Should().Be(false);
            captured.Properties.Should().ContainKey("character_sheet_field_count_bucket").WhoseValue.Should().Be("11-20");
            captured.Properties.Should().ContainKey("language_count_bucket").WhoseValue.Should().Be("1-5");
        }
        finally
        {
            AnalyticsService.Shutdown();
        }
    }

    private sealed class CapturingAnalyticsSink : IAnalyticsSink
    {
        public List<(string Name, IDictionary<string, object> Properties)> Events { get; } = new();

        public bool IsEnabled => true;

        public void Track(string eventName, IDictionary<string, object> properties)
        {
            Events.Add((eventName, new Dictionary<string, object>(properties)));
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

[CollectionDefinition("AnalyticsService static state", DisableParallelization = true)]
public class AnalyticsServiceCollection
{
}
