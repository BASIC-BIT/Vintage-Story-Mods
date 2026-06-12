using FluentAssertions;
using thebasics.ModSystems.Teleportation;

namespace thebasics.Tests.ModSystems.Teleportation;

public class TeleportBackUtilTests
{
    [Fact]
    public void IsExpired_WhenExpiryDisabled_ReturnsFalse()
    {
        var recorded = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        TeleportBackUtil.IsExpired(recorded.Ticks, 0, recorded.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenEntryIsOlderThanExpiry_ReturnsTrue()
    {
        var recorded = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        TeleportBackUtil.IsExpired(recorded.Ticks, 30, recorded.AddSeconds(31)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenEntryIsWithinExpiry_ReturnsFalse()
    {
        var recorded = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        TeleportBackUtil.IsExpired(recorded.Ticks, 30, recorded.AddSeconds(30)).Should().BeFalse();
    }
}
