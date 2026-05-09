using Xunit;

namespace QuartzDashboard.IntegrationTests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuartzDashboardIntegrationCollection
{
    public const string Name = "QuartzDashboard integration";
}
