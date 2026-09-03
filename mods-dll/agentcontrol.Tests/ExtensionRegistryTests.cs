using System.Text.Json;
using AgentControl.Abstractions;
using FluentAssertions;

namespace AgentControl.Tests;

public sealed class ExtensionRegistryTests
{
    [Fact]
    public void ThirdPartyOperation_RegistersInvokesAndUnregistersWithoutCoreChanges()
    {
        var registry = new ExtensionRegistry(null!);
        using (registry.Register(
            new AgentExtensionDescriptor(
                "agentcontrol.sample",
                "0.1.0",
                "selection.describe",
                "Test selection descriptor."),
            (context, arguments) => JsonSerializer.SerializeToElement(new { kind = "block", x = 12 })))
        {
            registry.List().Should().ContainSingle(item => item.Operation == "selection.describe");
            var result = registry.Invoke(
                "selection.describe",
                "call-1",
                JsonSerializer.SerializeToElement(new { }),
                TestContext.Current.CancellationToken);
            result.GetProperty("kind").GetString().Should().Be("block");
        }

        registry.List().Should().BeEmpty();
        Protocol.Methods.Should().NotContain("selection.describe");
    }

    [Fact]
    public void DuplicateOperation_IsRejected()
    {
        var registry = new ExtensionRegistry(null!);
        using var first = registry.Register(
            new AgentExtensionDescriptor("one", "1.0", "shared.operation", "First."),
            (_, _) => JsonSerializer.SerializeToElement(new { }));
        var registerAgain = () => registry.Register(
            new AgentExtensionDescriptor("two", "1.0", "shared.operation", "Second."),
            (_, _) => JsonSerializer.SerializeToElement(new { }));
        registerAgain.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }
}
