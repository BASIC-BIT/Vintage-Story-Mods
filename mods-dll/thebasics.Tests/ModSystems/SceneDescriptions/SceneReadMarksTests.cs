using System.Collections.Generic;
using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneReadMarksTests
{
    [Fact]
    public void RecentlyReadOldContentSurvivesTheNextReadAtCapacity()
    {
        var marks = new SceneReadMarksMessage();
        for (var index = 0; index < SceneReadMarks.MaxEntries; index++) SceneReadMarks.Set(marks, "pos-" + index, index + 100);
        SceneReadMarks.Set(marks, "old-content", 1);
        SceneReadMarks.Set(marks, "second-old-content", 2);
        marks.Marks.Count.Should().Be(SceneReadMarks.MaxEntries);
        SceneReadMarks.IsRead(marks.Marks, "old-content", 1).Should().BeTrue();
        SceneReadMarks.IsRead(marks.Marks, "second-old-content", 2).Should().BeTrue();
    }
    [Fact]
    public void PositionKeySeparatesDimensions()
    {
        SceneReadMarks.Key(new BlockPos(3, -4, 5, 0)).Should().Be("3/-4/5/0");
        SceneReadMarks.Key(new BlockPos(3, -4, 5, 2)).Should().NotBe(SceneReadMarks.Key(new BlockPos(3, -4, 5, 0)));
        SceneReadMarks.Key(null).Should().BeEmpty();
    }

    [Fact]
    public void OnlyTheStampThatWasMarkedCountsAsRead()
    {
        var marks = new Dictionary<string, long> { ["a"] = 100 };
        SceneReadMarks.IsRead(marks, "a", 100).Should().BeTrue();
        SceneReadMarks.IsRead(marks, "a", 101).Should().BeFalse();
        SceneReadMarks.IsRead(marks, "b", 100).Should().BeFalse();
        SceneReadMarks.IsRead(marks, "a", 0).Should().BeFalse();
        SceneReadMarks.IsRead(null, "a", 100).Should().BeFalse();
    }

    [Fact]
    public void SettingAZeroStampClearsTheMark()
    {
        var marks = new SceneReadMarksMessage { Marks = new Dictionary<string, long> { ["a"] = 100 } };
        SceneReadMarks.Set(marks, "a", 0);
        marks.Marks.Should().BeEmpty();
        marks.ReadOrder.Should().BeEmpty();
    }

    [Fact]
    public void MarksAreCappedByDroppingTheOldestReads()
    {
        var marks = new SceneReadMarksMessage();
        for (var index = 0; index <= SceneReadMarks.MaxEntries; index++) SceneReadMarks.Set(marks, "pos-" + index, index + 1);
        marks.Marks.Count.Should().Be(SceneReadMarks.MaxEntries);
        marks.Marks.Should().NotContainKey("pos-0");
        marks.Marks.Should().ContainKey("pos-" + SceneReadMarks.MaxEntries);
    }

    [Fact]
    public void ReadOrderSurvivesPersistenceAtCapacity()
    {
        var marks = new SceneReadMarksMessage();
        for (var index = 0; index < SceneReadMarks.MaxEntries; index++) SceneReadMarks.Set(marks, "pos-" + index, index + 100);
        SceneReadMarks.Set(marks, "pos-0", 100);
        marks = SerializerUtil.Deserialize<SceneReadMarksMessage>(SerializerUtil.Serialize(marks));
        SceneReadMarks.Set(marks, "old-content", 1);
        marks.Marks.Should().ContainKey("pos-0");
        marks.Marks.Should().NotContainKey("pos-1");
    }

    [Fact]
    public void LegacyMarksWithoutReadOrderMigrateOnNextRead()
    {
        var marks = new SceneReadMarksMessage
        {
            Marks = new Dictionary<string, long> { ["old"] = 1, ["new"] = 2 },
            ReadOrder = null
        };
        SceneReadMarks.Set(marks, "latest", 3);
        marks.ReadOrder.Should().ContainInOrder("old", "new", "latest");
    }

    [Fact]
    public void ReadStampPersistsInTheWorldButNotOnThePickedUpItem()
    {
        var data = new SceneDescriptionData { Body = "Some scene", ReadStamp = 1234567890123 };
        var tree = new TreeAttribute();
        data.WriteTo(tree, includeReadStamp: true);
        SceneDescriptionData.ReadFrom(tree).ReadStamp.Should().Be(1234567890123);

        var stack = new TreeAttribute();
        data.WriteTo(stack);
        SceneDescriptionData.ReadFrom(stack).ReadStamp.Should().Be(0);
    }

    [Fact]
    public void StampAssignsAFreshUnixMillisecondValue()
    {
        var data = new SceneDescriptionData();
        data.ReadStamp.Should().Be(0);
        data.Stamp();
        data.ReadStamp.Should().BeGreaterThan(1700000000000);
    }
}
