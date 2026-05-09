using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Defines a test collection that shares a single QuartzTestFixture across all
/// test classes. This prevents Quartz's static LogProvider from holding a
/// disposed LoggerFactory when a new test class creates its own host.
/// </summary>
[CollectionDefinition("QuartzDashboard")]
public sealed class QuartzTestCollection : ICollectionFixture<QuartzTestFixture>
{
}