using QuartzDashboard.Abstractions;
using QuartzDashboard.Internal;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class InMemoryFireHistoryStoreTests
{
    [Fact]
    public void Constructor_InitializesWithZeroCount()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 500, retentionHours: 24);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void RecordFire_IncrementsCount()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        store.RecordFire("jobs.a", "t.a", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), success: true);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void RecordFire_BeyondMaxCount_TrimsOldest()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 3, retentionHours: 0);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        for (var i = 0; i < 5; i++)
            store.RecordFire($"jobs.{i}", $"t.{i}", baseTime.AddMinutes(i), TimeSpan.FromSeconds(i + 1), success: true);

        Assert.Equal(3, store.Count);
        var records = store.GetRecent(10).ToList();
        Assert.Equal(3, records.Count);
        Assert.Equal("jobs.4", records[0].JobKey);
        Assert.Equal("jobs.3", records[1].JobKey);
        Assert.Equal("jobs.2", records[2].JobKey);
    }

    [Fact]
    public void RecordFire_WithRetention_PrunesExpired()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 100, retentionHours: 1);
        var expired = DateTimeOffset.UtcNow.AddHours(-2);
        var recent = DateTimeOffset.UtcNow;

        store.RecordFire("jobs.old", "t.old", expired, TimeSpan.FromSeconds(1), success: true);
        store.RecordFire("jobs.new", "t.new", recent, TimeSpan.FromSeconds(1), success: true);

        var records = store.GetRecent(10).ToList();
        Assert.Single(records);
        Assert.Equal("jobs.new", records[0].JobKey);
    }

    [Fact]
    public void GetRecent_ReturnsLatestFirst()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        for (var i = 0; i < 3; i++)
            store.RecordFire($"jobs.{i}", $"t.{i}", baseTime.AddMinutes(i), TimeSpan.FromSeconds(i + 1), success: true);

        var records = store.GetRecent(10).ToList();
        Assert.Equal(3, records.Count);
        Assert.True(records[0].FireTime > records[1].FireTime);
        Assert.True(records[1].FireTime > records[2].FireTime);
    }

    [Fact]
    public void GetRecent_WithOffset_SkipsRecords()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        for (var i = 0; i < 5; i++)
            store.RecordFire($"jobs.{i}", $"t.{i}", baseTime.AddMinutes(i), TimeSpan.FromSeconds(i + 1), success: true);

        var page1 = store.GetRecent(2, 0).ToList();
        var page2 = store.GetRecent(2, 2).ToList();

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.NotEqual(page1[0].JobKey, page2[0].JobKey);
    }

    [Fact]
    public void GetRecent_WithLimit_SlicesCorrectly()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        for (var i = 0; i < 10; i++)
            store.RecordFire($"jobs.{i}", $"t.{i}", baseTime.AddMinutes(i), TimeSpan.FromSeconds(i + 1), success: true);

        Assert.Equal(3, store.GetRecent(3).Count());
        Assert.Equal(5, store.GetRecent(5).Count());
    }

    [Fact]
    public void Clear_RemovesAllRecords()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        store.RecordFire("jobs.a", "t.a", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), success: true);
        store.RecordFire("jobs.b", "t.b", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), success: true);

        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Empty(store.GetRecent(10));
    }

    [Fact]
    public void OnFireRecorded_IsInvokedOnRecord()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        FireRecord? captured = null;

        store.OnFireRecorded += record => captured = record;
        store.RecordFire("jobs.test", "t.test", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(3), success: true, refireCount: 1);

        Assert.NotNull(captured);
        Assert.Equal("jobs.test", captured!.JobKey);
        Assert.Equal(TimeSpan.FromSeconds(3), captured.Duration);
        Assert.True(captured.Success);
        Assert.Equal(1, captured.RefireCount);
    }

    [Fact]
    public void OnFireRecorded_CapturesFailureDetails()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 10, retentionHours: 0);
        FireRecord? captured = null;

        store.OnFireRecorded += record => captured = record;
        store.RecordFire("jobs.fail", "t.fail", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), success: false,
            exceptionMessage: "Something broke", exceptionType: "InvalidOperationException");

        Assert.NotNull(captured);
        Assert.False(captured!.Success);
        Assert.Equal("Something broke", captured.ExceptionMessage);
        Assert.Equal("InvalidOperationException", captured.ExceptionType);
    }

    [Fact]
    public void RecordFire_WithMultipleSuccessFailures_TracksAll()
    {
        var store = new InMemoryFireHistoryStore(maxRecords: 100, retentionHours: 0);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-3);

        store.RecordFire("jobs.a", "t.a", baseTime.AddMinutes(0), TimeSpan.FromSeconds(1), success: true);
        store.RecordFire("jobs.b", "t.b", baseTime.AddMinutes(1), TimeSpan.FromSeconds(2), success: false,
            exceptionMessage: "err");
        store.RecordFire("jobs.c", "t.c", baseTime.AddMinutes(2), TimeSpan.FromSeconds(3), success: true);

        Assert.Equal(3, store.Count);

        var records = store.GetRecent(10).ToList();
        Assert.Equal(3, records.Count);
        Assert.True(records[0].Success);
        Assert.False(records[1].Success);
        Assert.True(records[2].Success);
    }
}
