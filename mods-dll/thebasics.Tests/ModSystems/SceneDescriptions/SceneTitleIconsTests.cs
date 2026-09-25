using Cairo;
using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneTitleIconsTests
{
    [Fact]
    public void ThrowingCustomIconLeavesCallerContextUsableForFallback()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var icons = new IconUtil(api);
        api.Gui.Icons.Returns(icons);
        icons.CustomIcons["broken"] = (ctx, _, _, _, _, _) =>
        {
            ctx.Operator = Operator.Clear;
            ctx.Translate(100, 100);
            ctx.Rectangle(0, 0, 1, 1);
            ctx.Clip();
            throw new InvalidOperationException("broken custom icon");
        };

        using var surface = new ImageSurface(Format.Argb32, 16, 16);
        using var caller = new Context(surface);
        caller.Operator = Operator.Source;

        SceneTitleIcons.Draw(api, caller, surface, "broken").Should().BeFalse();
        caller.Operator.Should().Be(Operator.Source);
        caller.SetSourceRGBA(1, 0, 0, 1);
        caller.Rectangle(0, 0, 16, 16);
        caller.Fill();
        surface.Flush();
        surface.Data[3].Should().Be(255, "fallback art should remain visible after the custom icon fails");
    }
}
