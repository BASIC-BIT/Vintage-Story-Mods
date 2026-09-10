using System.Collections.Generic;
using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneReadMarksTests
{
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
        var marks = new Dictionary<string, long> { ["a"] = 100 };
        SceneReadMarks.Set(marks, "a", 0);
        marks.Should().BeEmpty();
    }

    [Fact]
    public void MarksAreCappedByDroppingTheOldestStamps()
    {
        var marks = new Dictionary<string, long>();
        for (var index = 0; index <= SceneReadMarks.MaxEntries; index++) SceneReadMarks.Set(marks, "pos-" + index, index + 1);
        marks.Count.Should().Be(SceneReadMarks.MaxEntries);
        marks.Should().NotContainKey("pos-0");
        marks.Should().ContainKey("pos-" + SceneReadMarks.MaxEntries);
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
