using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Sqlite;

/// <summary>
/// DI extensions for registering the SQLite-backed fire-history store.
/// </summary>
public static class QuartzDashboardSqliteExtensions
{
    /// <summary>
    /// Registers <see cref="SqliteFireHistoryStore"/> as the singleton <see cref="IFireHistoryStore"/>,
    /// replacing any previous registration (e.g. the default in-memory store added by
    /// <c>AddQuartzDashboard()</c>). Call this <em>after</em> <c>AddQuartzDashboard()</c>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="databasePath">Path to the SQLite database file; created if missing.</param>
    /// <param name="maxHistory">Maximum number of records to retain (default 500).</param>
    /// <param name="retentionHours">Hours after which records are pruned. Use <c>0</c> to disable time-based pruning (default 24).</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddQuartzDashboardSqliteHistory(
        this IServiceCollection services,
        string databasePath,
        int maxHistory = 500,
        int retentionHours = 24)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.RemoveAll<IFireHistoryStore>();
        services.AddSingleton<IFireHistoryStore>(sp => new SqliteFireHistoryStore(
            databasePath,
            maxHistory,
            retentionHours,
            sp.GetRequiredService<ILogger<SqliteFireHistoryStore>>()));

        return services;
    }
}
