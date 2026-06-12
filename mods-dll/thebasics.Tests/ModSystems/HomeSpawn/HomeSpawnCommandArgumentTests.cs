using FluentAssertions;
using thebasics.ModSystems.HomeSpawn;

namespace thebasics.Tests.ModSystems.HomeSpawn;

public class HomeSpawnCommandArgumentTests
{
    [Theory]
    [InlineData("home")]
    [InlineData("sethome")]
    public void DefaultHomeCommands_AllowMissingHomeName(string commandName)
    {
        HomeSpawnSystem.GetHomeNameArgumentErrorCode(commandName, null).Should().BeNull();
    }

    [Fact]
    public void DeleteHome_RequiresExplicitHomeName()
    {
        HomeSpawnSystem.GetHomeNameArgumentErrorCode("delhome", null).Should().Be("home-name-required");
    }

    [Theory]
    [InlineData("delhome", "default")]
    [InlineData("delhome", "mine")]
    [InlineData("sethome", "default")]
    [InlineData("home", "mine")]
    public void HomeCommands_AcceptValidHomeNames(string commandName, string homeName)
    {
        HomeSpawnSystem.GetHomeNameArgumentErrorCode(commandName, homeName).Should().BeNull();
    }

    [Theory]
    [InlineData("sethome")]
    [InlineData("home")]
    [InlineData("delhome")]
    public void HomeCommands_RejectInvalidHomeNames(string commandName)
    {
        HomeSpawnSystem.GetHomeNameArgumentErrorCode(commandName, "bad/name").Should().Be("home-name-invalid");
    }
}
