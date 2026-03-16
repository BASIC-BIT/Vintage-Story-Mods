using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace thebasics.Tests.Fixtures;

/// <summary>
/// Combines AutoFixture with NSubstitute for automatic mock generation.
/// Usage: [Theory, AutoNSub] on test methods to auto-create mocks for interface parameters.
/// Use [Frozen] on parameters that should be shared (singleton) within the test.
/// </summary>
public class AutoNSubAttribute : AutoDataAttribute
{
    public AutoNSubAttribute()
        : base(() => new Fixture().Customize(new AutoNSubstituteCustomization()))
    {
    }
}
