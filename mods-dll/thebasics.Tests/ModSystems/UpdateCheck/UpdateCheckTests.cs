using FluentAssertions;
using Rules = thebasics.ModSystems.UpdateCheck.UpdateCheck;

// Not thebasics.Tests.ModSystems.UpdateCheck: a namespace with that name would shadow the class.
namespace thebasics.Tests.ModSystems;

public class UpdateCheckTests
{
    private static Rules.InstallInfo NoUpgrade => new(null, null);

    private static Rules.InstallInfo Upgrade(string version) => new(version, null);

    private static Rules.Release R(string version, params string[] tags) => new(version, tags);

    /// <summary>ModDB's real tag set for thebasics on 2026-11-26 (v1 hides retracted releases).</summary>
    private static List<Rules.Release> Today() =>
    [
        R("5.10.0-pre.1", "1.22.6"),
        R("5.9.1", "1.22.7"),
        R("5.9.0", "1.22.6"),
        R("5.8.1", "1.22.2"),
        R("5.7.1", "1.22.2"),
        R("5.7.0", "1.22.2"),
        R("5.6.0", "1.22.2"),
        R("5.5.0", "1.22.1"),
    ];

    // --- Decide ---------------------------------------------------------------------------------

    [Fact]
    public void ExactMatchUpgradeIsReported()
    {
        var d = Rules.Decide("5.9.0", "1.22.7", Upgrade("5.9.1"), null);

        d!.UpgradeVersion.Should().Be("5.9.1");
        d.TaggedForGameVersion.Should().BeNull();
        d.HasNotice.Should().BeTrue();
        Rules.NeedsReleaseList(Upgrade("5.9.1")).Should().BeFalse();
    }

    [Fact]
    public void UpToDateOnTaggedPatchIsQuiet()
    {
        Rules.NeedsReleaseList(NoUpgrade).Should().BeTrue();
        Rules.Decide("5.9.1", "1.22.7", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void UntaggedNewPatchFallsBackToNewestTaggedPatchOfSameMinor()
    {
        var d = Rules.Decide("5.9.0", "1.22.8", NoUpgrade, Today());

        d!.UpgradeVersion.Should().Be("5.9.1");
        d.TaggedForGameVersion.Should().Be("1.22.7");
    }

    [Fact]
    public void UntaggedNewPatchAlreadyOnNewestIsQuiet()
    {
        Rules.Decide("5.9.1", "1.22.8", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void ReleasePinnedToAnotherPatchIsNotOffered()
    {
        Rules.Decide("5.9.0", "1.22.6", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void UntaggedPatchBetweenTagsOnlyLooksDownward()
    {
        Rules.Decide("5.8.1", "1.22.4", NoUpgrade, Today()).Should().Be(Rules.Decision.None);

        // An older install on the same untagged patch is offered the newest release of 1.22.2, not 5.9.x.
        var d = Rules.Decide("5.6.0", "1.22.4", NoUpgrade, Today());
        d!.UpgradeVersion.Should().Be("5.8.1");
        d.TaggedForGameVersion.Should().Be("1.22.2");
    }

    [Fact]
    public void FallbackNeverCrossesAMinor()
    {
        Rules.Decide("5.9.1", "1.23.0", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
        Rules.Decide("5.5.0", "1.23.0", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void PreReleaseInstalledIsOfferedItsStable()
    {
        var d = Rules.Decide("5.10.0-pre.1", "1.22.6", Upgrade("5.10.0"), null);

        d!.UpgradeVersion.Should().Be("5.10.0");
    }

    [Fact]
    public void PreReleaseInstalledIsNotOfferedAnOlderStable()
    {
        Rules.Decide("5.10.0-pre.1", "1.22.6", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
        // Same with an untagged patch: the fallback 5.9.1 is older than the pre-release.
        Rules.Decide("5.10.0-pre.1", "1.22.8", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void PreReleaseAvailableIsIgnored()
    {
        // Row 10: the newest entry in R is a pre-release tagged for our patch; it is never offered.
        Rules.Decide("5.9.0", "1.22.6", NoUpgrade, Today()).Should().Be(Rules.Decision.None);

        // Row 11: a pre-release is the only thing tagged for our patch, so the patch counts as untagged.
        List<Rules.Release> releases = [R("5.11.0-pre.1", "1.22.9"), R("5.9.1", "1.22.7"), R("5.9.0", "1.22.6")];
        var d = Rules.Decide("5.9.0", "1.22.9", NoUpgrade, releases);
        d!.UpgradeVersion.Should().Be("5.9.1");
        d.TaggedForGameVersion.Should().Be("1.22.7");
    }

    [Fact]
    public void PreReleaseGameVersionDoesNotThrow()
    {
        Rules.Decide("5.9.1", "1.23.0-pre.2", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
        Rules.Decide("5.9.0", "1.23.0-pre.2", Upgrade("5.9.1"), null)!.UpgradeVersion.Should().Be("5.9.1");
        // A stable 1.23.0 tag is newer than 1.23.0-pre.2, so it is not a fallback for it.
        Rules.Decide("5.9.0", "1.23.0-pre.2", NoUpgrade, [R("5.12.0", "1.23.0")]).Should().Be(Rules.Decision.None);
    }

    [Theory]
    [InlineData(4041)]
    [InlineData(4001)]
    [InlineData(4002)]
    [InlineData(4031)]
    [InlineData(4032)]
    public void ErrorCodesOtherThanInstalledRetractedAreQuietAndSkipTheReleaseList(int errorCode)
    {
        var info = new Rules.InstallInfo("5.9.1", errorCode);

        Rules.NeedsReleaseList(info).Should().BeFalse();
        Rules.Decide("5.9.0", "1.22.7", info, null).Should().Be(Rules.Decision.None);
    }

    [Theory]
    [InlineData(4101)]
    [InlineData(4102)]
    public void InstalledRetractedCodesGiveTheOrdinaryUpdateNotice(int errorCode)
    {
        var withUpgrade = new Rules.InstallInfo("5.9.1", errorCode);
        Rules.NeedsReleaseList(withUpgrade).Should().BeFalse();
        Rules.Decide("5.9.0", "1.22.7", withUpgrade, null).Should().Be(new Rules.Decision("5.9.1", null));

        var withoutUpgrade = new Rules.InstallInfo(null, errorCode);
        Rules.NeedsReleaseList(withoutUpgrade).Should().BeTrue();
        Rules.Decide("5.9.0", "1.22.8", withoutUpgrade, Today()).Should().Be(new Rules.Decision("5.9.1", "1.22.7"));
    }

    [Fact]
    public void NullInstallInfoMeansFailureNotQuiet()
    {
        Rules.NeedsReleaseList(null).Should().BeFalse();
        Rules.Decide("5.9.0", "1.22.7", null, Today()).Should().BeNull();
    }

    [Fact]
    public void MissingReleaseListMeansFailure()
    {
        Rules.Decide("5.9.0", "1.22.8", NoUpgrade, null).Should().BeNull();
    }

    [Fact]
    public void UnparseableOrMissingVersionsAreSkipped()
    {
        // Garbage / null recommendedUpgrade falls through to the release list.
        Rules.Decide("5.9.0", "1.22.8", Upgrade("not-a-version"), Today())!.UpgradeVersion.Should().Be("5.9.1");
        Rules.Decide("5.9.0", "1.22.8", Upgrade(null!), Today())!.UpgradeVersion.Should().Be("5.9.1");

        // CheckAsync must fetch B for the same values Decide falls through on, or Decide gets no list.
        Rules.NeedsReleaseList(Upgrade("not-a-version")).Should().BeTrue();
        Rules.NeedsReleaseList(Upgrade("")).Should().BeTrue();

        // A numeric part too large for an int makes SemVer.TryParse throw; it must be skipped instead.
        Rules.NeedsReleaseList(Upgrade("5.99999999999.0")).Should().BeTrue();
        Rules.Decide("5.9.0", "1.22.8", Upgrade("5.99999999999.0"), Today())!.UpgradeVersion.Should().Be("5.9.1");
        Rules.Decide("5.9.0", "1.22.8", NoUpgrade, [.. Today(), R("5.9.9", "1.22.99999999999")])!.UpgradeVersion.Should().Be("5.9.1");

        // A pre-release recommendation is ignored (stable only) and the release list decides.
        Rules.NeedsReleaseList(Upgrade("5.10.0-pre.2")).Should().BeTrue();
        Rules.Decide("5.9.0", "1.22.8", Upgrade("5.10.0-pre.2"), Today())!.UpgradeVersion.Should().Be("5.9.1");
        Rules.Decide("5.10.0-pre.1", "1.22.7", Upgrade("5.10.0-pre.2"), Today()).Should().Be(Rules.Decision.None);

        List<Rules.Release> releases =
        [
            R("garbage", "1.22.7"),
            R(null!, "1.22.7"),
            new("5.9.5", null!),
            R("5.9.4", "nope", null!),
            R("5.9.1", "1.22.7"),
        ];
        var d = Rules.Decide("5.9.0", "1.22.8", NoUpgrade, releases);
        d!.UpgradeVersion.Should().Be("5.9.1");

        // Null or garbage running versions never throw.
        Rules.Decide(null!, "1.22.8", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
        Rules.Decide("5.9.0", null!, NoUpgrade, Today()).Should().Be(Rules.Decision.None);
        Rules.Decide("5.9.0", "garbage", NoUpgrade, Today()).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void RecommendedUpgradeNotNewerThanInstalledIsQuiet()
    {
        Rules.Decide("5.9.1", "1.22.7", Upgrade("5.9.1"), null).Should().Be(Rules.Decision.None);
        Rules.Decide("5.9.1", "1.22.7", Upgrade("5.9.0"), null).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void WholeLineTagListBehavesLikeAnyOtherTagSet()
    {
        List<Rules.Release> releases =
        [
            R("5.4.0", "1.21.0-pre.1", "1.21.0-pre.2", "1.21.0-rc.1", "1.21.0", "1.21.1-rc.1", "1.21.1",
              "1.21.2", "1.21.3", "1.21.4", "1.21.5", "1.21.6-rc.1", "1.21.6"),
            R("5.2.3", "1.20.12", "1.21.0", "1.21.1"),
        ];

        var d = Rules.Decide("5.2.3", "1.21.7", NoUpgrade, releases);
        d!.UpgradeVersion.Should().Be("5.4.0");
        d.TaggedForGameVersion.Should().Be("1.21.6");

        Rules.Decide("5.2.3", "1.21.3", NoUpgrade, releases).Should().Be(Rules.Decision.None);
    }

    [Fact]
    public void SemVerOrdersPreReleasesTheWayModDbDoes()
    {
        // U4: ProperVersion.SemVer is what Decide compares with. dev < pre < rc < stable.
        ProperVersion.SemVer.Parse("5.10.0-dev.1").Should().BeLessThan(ProperVersion.SemVer.Parse("5.10.0-pre.1"));
        ProperVersion.SemVer.Parse("5.10.0-pre.2").Should().BeLessThan(ProperVersion.SemVer.Parse("5.10.0-rc.1"));
        ProperVersion.SemVer.Parse("5.10.0-rc.1").Should().BeLessThan(ProperVersion.SemVer.Parse("5.10.0"));
        ProperVersion.SemVer.Parse("1.21.0-rc.10").Should().BeGreaterThan(ProperVersion.SemVer.Parse("1.21.0-rc.2"));
        ProperVersion.SemVer.Parse("5.9.1").Should().BeLessThan(ProperVersion.SemVer.Parse("5.10.0-pre.1"));
    }

    // --- Parsing --------------------------------------------------------------------------------

    [Fact]
    public void ParsesLiveInstallInformationPayloads()
    {
        // Live bodies from mods.vintagestory.at on 2026-11-26.
        var withUpgrade = Rules.ParseInstallInfo(
            """{"data":{"thebasics":{"recommendedUpgrade":"5.9.1","fileName":"thebasics-v5.9.0.zip","fileUrl":"\/download\/114262\/thebasics-v5.9.0.zip"}}}""");
        withUpgrade.Should().Be(new Rules.InstallInfo("5.9.1", null));

        var without = Rules.ParseInstallInfo(
            """{"data":{"thebasics":{"fileName":"thebasics-v5.9.1.zip","fileUrl":"\/download\/119463\/thebasics-v5.9.1.zip"}}}""");
        without.Should().Be(new Rules.InstallInfo(null, null));

        var notFound = Rules.ParseInstallInfo("""{"data":{"thebasics":{"errorCode":4041}}}""");
        notFound.Should().Be(new Rules.InstallInfo(null, 4041));
    }

    [Fact]
    public void ParsesErrorCodeAsNumberOrNumericString()
    {
        // Keys per vsmoddb lib/api/public/mods.php: a 4101 answer keeps recommendedUpgrade.
        Rules.ParseInstallInfo("""{"data":{"thebasics":{"recommendedUpgrade":"5.9.1","errorCode":4101}}}""")
            .Should().Be(new Rules.InstallInfo("5.9.1", 4101));

        // The v1 API quotes its status code; accept a quoted errorCode too.
        Rules.ParseInstallInfo("""{"data":{"thebasics":{"errorCode":"4101"}}}""")
            .Should().Be(new Rules.InstallInfo(null, 4101));
        Rules.ParseInstallInfo("""{"data":{"thebasics":{"errorCode":"nope"}}}""")
            .Should().Be(new Rules.InstallInfo(null, null));
    }

    [Fact]
    public void ReleaseListWithNoReadableVersionsIsAFailureNotUpToDate()
    {
        // What a renamed modversion field looks like after parsing.
        var unreadable = new List<Rules.Release> { R(null, "1.22.7"), R(null, "1.22.6") };

        Rules.Decide("5.9.0", "1.22.8", NoUpgrade, unreadable).Should().BeNull();
        // A mod with published releases never has an empty list, so empty is a failure too.
        Rules.Decide("5.9.0", "1.22.8", NoUpgrade, new List<Rules.Release>()).Should().BeNull();
    }

    [Fact]
    public void ParsesV1ReleaseListAndIgnoresHtmlFields()
    {
        const string json = """
            {"mod":{"modid":1,"name":"The BASICs","text":"<p>desc</p>","releases":[
              {"releaseid":3,"tags":["1.22.6"],"modidstr":"thebasics","modversion":"5.10.0-pre.1","changelog":"<ul><li>x</li></ul>"},
              {"releaseid":2,"tags":["1.22.7"],"modidstr":"thebasics","modversion":"5.9.1","changelog":"<p>y</p>"},
              {"releaseid":1,"tags":["1.21.0-rc.2","1.21.0"],"modidstr":"thebasics","modversion":"5.4.0","changelog":null}
            ]},"statuscode":"200"}
            """;

        var releases = Rules.ParseReleases(json);

        releases.Should().HaveCount(3);
        releases![0].Should().BeEquivalentTo(R("5.10.0-pre.1", "1.22.6"));
        releases[1].Should().BeEquivalentTo(R("5.9.1", "1.22.7"));
        releases[2].Tags.Should().Equal("1.21.0-rc.2", "1.21.0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("[]")]
    [InlineData("""{"data":null}""")]
    [InlineData("""{"data":{"thebasics":[]}}""")]
    [InlineData("""{"mod":{"name":"x"}}""")]
    [InlineData("""{"mod":{"releases":null}}""")]
    public void MalformedJsonReturnsNull(string json)
    {
        Rules.ParseInstallInfo(json).Should().BeNull();
        Rules.ParseReleases(json).Should().BeNull();
        Rules.ParseInstallInfo(null!).Should().BeNull();
        Rules.ParseReleases(null!).Should().BeNull();
    }

    // --- Decision -------------------------------------------------------------------------------

    [Fact]
    public void KeyChangesWithVersion()
    {
        var a = new Rules.Decision("5.9.1", null);
        var b = new Rules.Decision("5.9.2", null);
        var a2 = new Rules.Decision("5.9.1", "1.22.7");

        a.Key.Should().NotBe(b.Key);
        a.Key.Should().Be(a2.Key);
        Rules.Decision.None.HasNotice.Should().BeFalse();
    }
}
