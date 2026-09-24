using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionEntriesTests
{
    [Fact]
    public void LegacyFlatMarkerBecomesOneStableEntry()
    {
        var saved = new TreeAttribute();
        new SceneDescriptionData { Title = "A warning", Body = "Keep out", AuthorUid = "writer", ReadStamp = 123 }
            .WriteTo(saved, includeReadStamp: true);

        var firstLoad = SceneDescriptionEntries.ReadFrom(saved);
        var secondLoad = SceneDescriptionEntries.ReadFrom(saved);

        firstLoad.Entries.Should().ContainSingle();
        firstLoad.Primary.Id.Should().Be(SceneDescriptionEntries.LegacyId);
        secondLoad.Primary.Id.Should().Be(firstLoad.Primary.Id);
        firstLoad.Primary.Data.Title.Should().Be("A warning");
        firstLoad.Primary.Data.Body.Should().Be("Keep out");
        firstLoad.Primary.Data.AuthorUid.Should().Be("writer");
        firstLoad.Primary.Data.ReadStamp.Should().Be(123);
    }

    [Fact]
    public void SeveralEntriesRoundTripWithIndependentDataAndOptionalReadStamps()
    {
        var entries = SceneDescriptionEntries.ReadFrom(new TreeAttribute());
        entries.Primary.Data.Title = "First";
        entries.Primary.Data.AuthorUid = "alice";
        entries.Primary.Data.ReadStamp = 123;
        var second = entries.Add(new SceneDescriptionData { Title = "Second", AuthorUid = "bob", ReadStamp = 456 });

        second.Should().NotBeNull();
        second!.Id.Should().NotBe(entries.Primary.Id);
        var world = new TreeAttribute();
        entries.WriteTo(world, includeReadStamps: true);
        var restored = SceneDescriptionEntries.ReadFrom(world);
        restored.Entries.Select(entry => entry.Id).Should().Equal(entries.Entries.Select(entry => entry.Id));
        restored.Entries.Select(entry => entry.Data.Title).Should().Equal("First", "Second");
        restored.Entries.Select(entry => entry.Data.AuthorUid).Should().Equal("alice", "bob");
        restored.Entries.Select(entry => entry.Data.ReadStamp).Should().Equal(123, 456);

        var item = new TreeAttribute();
        entries.WriteTo(item);
        SceneDescriptionEntries.ReadFrom(item).Entries.Select(entry => entry.Data.ReadStamp).Should().Equal(0, 0);
    }

    [Fact]
    public void EntriesCanBeFoundAndRemovedWithoutEmptyingTheMarker()
    {
        var entries = new SceneDescriptionEntries();
        var added = entries.Add(new SceneDescriptionData { Title = "Another voice" });

        entries.Find(added!.Id).Should().BeSameAs(added);
        entries.Remove(entries.Primary.Id).Should().BeTrue();
        entries.Primary.Should().BeSameAs(added);
        entries.Remove(added.Id).Should().BeFalse();
        entries.Find("missing").Should().BeNull();
    }

    [Fact]
    public void MalformedCountAndDuplicateIdsCannotExceedTheLimitOrAliasEntries()
    {
        var saved = new TreeAttribute();
        var children = new TreeAttribute();
        children.SetInt("count", int.MaxValue);
        for (var index = 0; index < SceneDescriptionEntries.MaxEntries + 1; index++)
        {
            var child = new TreeAttribute();
            child.SetString("id", "legacy");
            child.SetString(SceneDescriptionData.TitleAttribute, "Entry " + index);
            children[index.ToString()] = child;
        }
        saved["sceneEntries"] = children;

        var restored = SceneDescriptionEntries.ReadFrom(saved);

        restored.Entries.Should().HaveCount(SceneDescriptionEntries.MaxEntries);
        restored.Entries.Select(entry => entry.Id).Should().OnlyHaveUniqueItems();
        restored.Entries.Select(entry => entry.Data.Title).Should().NotContain("Entry " + SceneDescriptionEntries.MaxEntries);
        SceneDescriptionEntries.ReadFrom(saved).Entries.Select(entry => entry.Id)
            .Should().Equal(restored.Entries.Select(entry => entry.Id));
        restored.Add(new SceneDescriptionData { Title = "Too many" }).Should().BeNull();
    }
}
