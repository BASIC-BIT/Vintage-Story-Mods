using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerCustomColorContractTests
{
    [Fact]
    public void CustomColorSurvivesItemAttributesWithoutReplacingItsPresetFallback()
    {
        var tree = new TreeAttribute();
        tree.SetInt("sceneColor", (int)SceneMarkerColor.Blue);
        tree.SetString("sceneCustomColor", "#a153cf");
        var data = SceneDescriptionData.ReadFrom(tree);
        Assert.Equal(SceneMarkerColor.Blue, data.Color);
        var stored = new TreeAttribute();
        data.Clone().WriteTo(stored);
        Assert.Equal("#A153CF", stored.GetString("sceneCustomColor"));
        Assert.Equal((int)SceneMarkerColor.Blue, stored.GetInt("sceneColor"));
    }
}
