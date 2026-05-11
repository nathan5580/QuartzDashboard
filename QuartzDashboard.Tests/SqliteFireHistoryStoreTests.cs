using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz.Logging;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Sqlite;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class SqliteFireHistoryStoreTests
{
    [Fact]
    public void AddQuartzDashboardSqliteHistory_ReplacesDefaultInMemoryStore()
    {
        var sqlitePath = GetArtifactPath("replace");

        try
        {
            ResetQuartzLogProvider();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddQuartzDashboard(options =>
            {
                options.UseSignalR = false;
            });
            services.AddQuartzDashboardSqliteHistory(sqlitePath);

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IFireHistoryStore>();

            Assert.IsType<SqliteFireHistoryStore>(store);
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_PersistsRecordsAcrossInstances()
    {
        var sqlitePath = GetArtifactPath("persistence");
        var fireTime = DateTimeOffset.UtcNow;

        try
        {
            ResetQuartzLogProvider();

            using (var provider = CreateProvider(sqlitePath))
            {
                var store = provider.GetRequiredService<IFireHistoryStore>();
                store.RecordFire("jobs.email", "triggers.daily", fireTime, TimeSpan.FromSeconds(5), success: false,
                    refireCount: 2, exceptionMessage: "boom", exceptionType: "InvalidOperationException");

                var initialRecord = Assert.Single(store.GetRecent(10));
                Assert.Equal(1, store.Count);
                Assert.Equal("jobs.email", initialRecord.JobKey);
                Assert.False(initialRecord.Success);
            }

            using (var provider = CreateProvider(sqlitePath))
            {
                var store = provider.GetRequiredService<IFireHistoryStore>();
                var persisted = Assert.Single(store.GetRecent(10));

                Assert.Equal(1, store.Count);
                Assert.Equal("jobs.email", persisted.JobKey);
                Assert.Equal("triggers.daily", persisted.TriggerKey);
                Assert.Equal(fireTime.ToUnixTimeMilliseconds(), persisted.FireTime.ToUnixTimeMilliseconds());
                Assert.Equal(TimeSpan.FromSeconds(5), persisted.Duration);
                Assert.False(persisted.Success);
                Assert.Equal(2, persisted.RefireCount);
                Assert.Equal("boom", persisted.ExceptionMessage);
                Assert.Equal("InvalidOperationException", persisted.ExceptionType);
            }
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_RecordAndRetrieveFireHistory_Works()
    {
        var sqlitePath = GetArtifactPath("retrieve");
        var firstFire = DateTimeOffset.UtcNow.AddMinutes(-2);
        var secondFire = DateTimeOffset.UtcNow.AddMinutes(-1);

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 10, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            store.RecordFire("jobs.first", "triggers.first", firstFire, TimeSpan.FromSeconds(1), success: true);
            store.RecordFire("jobs.second", "triggers.second", secondFire, TimeSpan.FromSeconds(2), success: false,
                exceptionMessage: "failed", exceptionType: "Exception");

            var records = store.GetRecent(10).ToList();

            Assert.Equal(2, records.Count);
            Assert.Equal("jobs.second", records[0].JobKey);
            Assert.Equal("triggers.second", records[0].TriggerKey);
            Assert.False(records[0].Success);
            Assert.Equal("jobs.first", records[1].JobKey);
            Assert.True(records[1].Success);
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_MaxCountPruning_RemovesOldestRecords()
    {
        var sqlitePath = GetArtifactPath("max-count");
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 3, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            for (var i = 0; i < 5; i++)
                store.RecordFire($"jobs.{i}", $"triggers.{i}", baseTime.AddMinutes(i), TimeSpan.FromSeconds(i + 1), success: true);

            var records = store.GetRecent(10).ToList();

            Assert.Equal(3, store.Count);
            Assert.Equal(3, records.Count);
            Assert.Equal(["jobs.4", "jobs.3", "jobs.2"], records.Select(r => r.JobKey).ToArray());
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_TtlRetentionPruning_RemovesExpiredRecords()
    {
        var sqlitePath = GetArtifactPath("retention");
        var expiredFire = DateTimeOffset.UtcNow.AddHours(-2);
        var retainedFire = DateTimeOffset.UtcNow.AddMinutes(-5);

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 10, retentionHours: 1);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            store.RecordFire("jobs.expired", "triggers.expired", expiredFire, TimeSpan.FromSeconds(1), success: true);
            store.RecordFire("jobs.retained", "triggers.retained", retainedFire, TimeSpan.FromSeconds(1), success: true);

            var record = Assert.Single(store.GetRecent(10));
            Assert.Equal(1, store.Count);
            Assert.Equal("jobs.retained", record.JobKey);
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_Clear_RemovesAllRecords()
    {
        var sqlitePath = GetArtifactPath("clear");

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 10, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            store.RecordFire("jobs.one", "triggers.one", DateTimeOffset.UtcNow.AddMinutes(-2), TimeSpan.FromSeconds(1), success: true);
            store.RecordFire("jobs.two", "triggers.two", DateTimeOffset.UtcNow.AddMinutes(-1), TimeSpan.FromSeconds(1), success: true);
            store.Clear();

            Assert.Equal(0, store.Count);
            Assert.Empty(store.GetRecent(10));
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_Count_ReturnsCorrectValue()
    {
        var sqlitePath = GetArtifactPath("count");

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 10, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            Assert.Equal(0, store.Count);

            store.RecordFire("jobs.one", "triggers.one", DateTimeOffset.UtcNow.AddMinutes(-2), TimeSpan.FromSeconds(1), success: true);
            Assert.Equal(1, store.Count);

            store.RecordFire("jobs.two", "triggers.two", DateTimeOffset.UtcNow.AddMinutes(-1), TimeSpan.FromSeconds(1), success: true);
            Assert.Equal(2, store.Count);
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public async Task SqliteFireHistoryStore_ConcurrentWrites_DoNotCorruptData()
    {
        var sqlitePath = GetArtifactPath("concurrency");

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 100, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() =>
                store.RecordFire($"jobs.{i}", $"triggers.{i}", DateTimeOffset.UtcNow.AddMilliseconds(i), TimeSpan.FromMilliseconds(i + 1), success: true))));

            var records = store.GetRecent(100).ToList();

            Assert.Equal(50, store.Count);
            Assert.Equal(50, records.Count);
            Assert.Equal(50, records.Select(r => r.JobKey).Distinct().Count());
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_GetRecentWithJobFilter_OnlyReturnsMatchingJob()
    {
        var sqlitePath = GetArtifactPath("filter");
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 100, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            store.RecordFire("jobs.alpha", "t.alpha", baseTime.AddMinutes(1), TimeSpan.FromSeconds(1), success: true);
            store.RecordFire("jobs.beta",  "t.beta",  baseTime.AddMinutes(2), TimeSpan.FromSeconds(1), success: true);
            store.RecordFire("jobs.alpha", "t.alpha", baseTime.AddMinutes(3), TimeSpan.FromSeconds(1), success: false, exceptionMessage: "boom");
            store.RecordFire("jobs.beta",  "t.beta",  baseTime.AddMinutes(4), TimeSpan.FromSeconds(1), success: true);

            var alphaOnly = store.GetRecent(100, 0, "jobs.alpha").ToList();
            Assert.Equal(2, alphaOnly.Count);
            Assert.All(alphaOnly, r => Assert.Equal("jobs.alpha", r.JobKey));

            Assert.Equal(2, store.CountFiltered("jobs.alpha"));
            Assert.Equal(2, store.CountFiltered("jobs.beta"));
            Assert.Equal(4, store.CountFiltered(null));
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_GetRecentWithJobFilter_IsCaseInsensitive()
    {
        var sqlitePath = GetArtifactPath("filter-case");

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 100, retentionHours: 0);
            var store = provider.GetRequiredService<IFireHistoryStore>();

            store.RecordFire("Jobs.Alpha", "t.alpha", DateTimeOffset.UtcNow.AddMinutes(-1), TimeSpan.FromSeconds(1), success: true);

            Assert.Single(store.GetRecent(100, 0, "jobs.alpha"));
            Assert.Equal(1, store.CountFiltered("JOBS.ALPHA"));
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    [Fact]
    public void SqliteFireHistoryStore_DatabaseFile_IsAutoCreated()
    {
        var sqlitePath = GetArtifactPath("auto-create");
        DeleteIfExists(sqlitePath);

        try
        {
            ResetQuartzLogProvider();

            using var provider = CreateProvider(sqlitePath, maxHistory: 10, retentionHours: 0);
            _ = provider.GetRequiredService<IFireHistoryStore>();

            Assert.True(File.Exists(sqlitePath));
        }
        finally
        {
            ResetQuartzLogProvider();
            DeleteIfExists(sqlitePath);
        }
    }

    private static ServiceProvider CreateProvider(string sqlitePath, int maxHistory = 10, int retentionHours = 0)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartzDashboard(options =>
        {
            options.UseSignalR = false;
            options.MaxFireHistory = maxHistory;
            options.HistoryRetentionHours = retentionHours;
        });
        services.AddQuartzDashboardSqliteHistory(sqlitePath, maxHistory, retentionHours);

        return services.BuildServiceProvider();
    }

    private static string GetArtifactPath(string testName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestArtifacts");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"sqlite-history-{testName}-{Guid.NewGuid():N}.db");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void ResetQuartzLogProvider()
    {
        var currentField = typeof(LogProvider).GetField("s_currentLogProvider", BindingFlags.Static | BindingFlags.NonPublic);
        currentField?.SetValue(null, null);
    }
}
