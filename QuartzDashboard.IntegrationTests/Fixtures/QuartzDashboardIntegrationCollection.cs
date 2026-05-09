using Xunit;

namespace QuartzDashboard.IntegrationTests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuartzDashboardIntegrationCollection : ICollectionFixture<TestWebAppFactory>
{
    public const string Name = "QuartzDashboard integration";
}
