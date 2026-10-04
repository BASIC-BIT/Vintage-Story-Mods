using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigPersistenceTests
{
    [Fact]
    public void FailedWriteKeepsRuntimeValuesAndSuccessfulWritePrecedesMutation()
    {
        var failingApi = Substitute.For<ICoreServerAPI>();
        var active = new ModConfig();
        active.InitializeDefaultsIfNeeded();
        var draft = new ModConfig();
        draft.InitializeDefaultsIfNeeded();
        draft.EnableChatter = !active.EnableChatter;
        var system = new PersistenceProbe { API = failingApi, Config = active };
        var oldValue = active.EnableChatter;
        failingApi.When(x => x.StoreModConfig(Arg.Any<ModConfig>(), "the_basics.json"))
            .Do(_ => throw new IOException("disk full"));

        system.Save(draft, out var error).Should().BeFalse();
        error.Should().Contain("not been applied");
        active.EnableChatter.Should().Be(oldValue);

        var successfulApi = Substitute.For<ICoreServerAPI>();
        successfulApi.When(x => x.StoreModConfig(Arg.Any<ModConfig>(), "the_basics.json"))
            .Do(_ => active.EnableChatter.Should().Be(oldValue));
        system.API = successfulApi;
        system.Save(draft, out error).Should().BeTrue();
        error.Should().BeNull();
        active.EnableChatter.Should().Be(draft.EnableChatter);
        successfulApi.Received(1).StoreModConfig(draft, "the_basics.json");
    }

    private sealed class PersistenceProbe : BaseBasicModSystem
    {
        protected override void BasicStartServerSide() { }
        public bool Save(ModConfig draft, out string error) => TryPersistConfigDraft(draft, out error);
    }
}
