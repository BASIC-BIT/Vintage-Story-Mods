using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionLockTests
{
    [Theory]
    [InlineData("creator", false, true, false, true, true)]
    [InlineData("other", false, true, false, false, false)]
    [InlineData("admin", true, true, false, true, true)]
    [InlineData("creator", false, false, false, false, false)]
    [InlineData("admin", true, false, false, false, false)]
    [InlineData(null, true, true, false, false, false)]
    public void LockedMarker_RestrictsEditBreakAndUnlock(
        string? uid, bool admin, bool claim, bool edit, bool remove, bool unlock)
    {
        var marker = new SceneDescriptionData { AuthorUid = "creator", LockItemCode = "game:padlock-copper" };
        marker.CanEdit(uid, claim).Should().Be(edit);
        marker.CanBreak(uid, admin, claim).Should().Be(remove);
        marker.CanUnlock(uid, admin, claim).Should().Be(unlock);
        marker.CanLock(uid, claim).Should().BeFalse();
    }

    [Theory]
    [InlineData("creator", true, true, true)]
    [InlineData("other", true, true, false)]
    [InlineData("creator", false, false, false)]
    [InlineData(null, true, false, false)]
    public void UnlockedMarker_AllowsClaimEditorsButOnlyCreatorCanLock(string? uid, bool claim, bool edit, bool canLock)
    {
        var marker = new SceneDescriptionData { AuthorUid = "creator" };
        marker.CanEdit(uid, claim).Should().Be(edit);
        marker.CanLock(uid, claim).Should().Be(canLock);
        marker.CanUnlock(uid, true, claim).Should().BeFalse();
    }

    [Fact]
    public void Edits_DoNotTransferCreatorOrLockState()
    {
        var marker = new SceneDescriptionData { AuthorUid = "creator", AuthorName = "First", LockItemCode = "game:padlock-copper" };
        marker.ApplyText(new SceneDescriptionData { Title = "New", Body = "Changed", AuthorUid = "attacker", AuthorName = "Other" });
        marker.EstablishCreator("editor", "Editor");
        marker.AuthorUid.Should().Be("creator");
        marker.AuthorName.Should().Be("First");
        marker.LockItemCode.Should().Be("game:padlock-copper");
        marker.Body.Should().Be("Changed");
    }

    [Fact]
    public void LockAndCreator_SurviveWorldAndItemRoundTripAndClone()
    {
        var original = new SceneDescriptionData { AuthorUid = "creator", AuthorName = "First", LockItemCode = "game:padlock-copper", Body = "Narration" };
        var tree = new TreeAttribute();
        original.WriteTo(tree);
        var restored = SceneDescriptionData.ReadFrom(tree);
        restored.Should().BeEquivalentTo(original);
        restored.Clone().Should().BeEquivalentTo(original);
        restored.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void LegacyMarker_IsUnlockedAndKeepsRecordedAuthor()
    {
        var tree = new TreeAttribute();
        tree.SetString("sceneAuthorUid", "legacy-author");
        var restored = SceneDescriptionData.ReadFrom(tree);
        restored.EstablishCreator("placer", "Placer");
        restored.IsLocked.Should().BeFalse();
        restored.AuthorUid.Should().Be("legacy-author");
    }

    [Fact]
    public void NewMarker_AssignsCreatorOnlyOnce()
    {
        var marker = new SceneDescriptionData();
        marker.EstablishCreator("first", "First");
        marker.EstablishCreator("second", "Second");
        marker.AuthorUid.Should().Be("first");
    }
}
