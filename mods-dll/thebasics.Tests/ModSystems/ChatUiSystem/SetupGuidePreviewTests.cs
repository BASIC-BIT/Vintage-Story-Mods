using Newtonsoft.Json.Linq;
using NSubstitute;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupGuidePreviewTests
{
    [Fact]
    public void DetachedPropertiesKeepSourceAttributesAndTexturesUntouched()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var attributes = new JsonObject(JObject.Parse("{value: 1}"));
        var behavior = new JsonObject(JObject.Parse("{code: 'extraskinnable', options: {value: 1}}"));
        var texture = new CompositeTexture(new AssetLocation("game:entity/humanoid/seraph-naked-hairless"));
        texture.Bake(api.Assets);
        texture.Baked.TextureSubId = 41;
        var source = new EntityProperties
        {
            Code = new AssetLocation("game:player"),
            Attributes = attributes,
            Client = new EntityClientProperties([behavior, new JsonObject(JObject.Parse("{code: 'playerinventory'}"))], null)
            {
                Textures = new Dictionary<string, CompositeTexture> { ["seraph"] = texture },
                Shape = new CompositeShape { Base = new AssetLocation("game:entity/humanoid/seraph-faceless") }
            }
        };

        var copy = SetupGuidePreview.CopyProperties(source, api);
        Assert.Same(attributes, source.Attributes);
        Assert.NotSame(source.Attributes.Token, copy.Attributes.Token);
        Assert.NotSame(source.Client.Textures["seraph"], copy.Client.Textures["seraph"]);
        Assert.NotSame(texture.Baked, copy.Client.Textures["seraph"].Baked);
        Assert.Equal(41, copy.Client.Textures["seraph"].Baked.TextureSubId);
        Assert.Single(copy.Client.BehaviorsAsJsonObj);
        Assert.NotSame(source.Client.BehaviorsAsJsonObj[0].Token, copy.Client.BehaviorsAsJsonObj[0].Token);

        copy.Attributes.Token["value"] = 2;
        copy.Client.BehaviorsAsJsonObj[0].Token["options"]["value"] = 2;
        copy.Client.Textures["seraph"].Baked.TextureSubId = 99;
        Assert.Equal(1, source.Attributes["value"].AsInt());
        Assert.Equal(1, source.Client.BehaviorsAsJsonObj[0]["options"]["value"].AsInt());
        Assert.Equal(41, texture.Baked.TextureSubId);
    }
}
