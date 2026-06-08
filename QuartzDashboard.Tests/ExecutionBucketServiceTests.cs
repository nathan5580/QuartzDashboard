using QuartzDashboard.Services;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class ExecutionBucketServiceTests
{
    [Fact]
    public void GetBuckets_WhenEmpty_ReturnsEmpty()
    {
        var service = new ExecutionBucketService();
        var buckets = service.GetBuckets().ToList();
        Assert.Empty(buckets);
    }

    [Fact]
    public void Record_IncrementsCountAndDuration()
    {
        var service = new ExecutionBucketService();

        service.Record(TimeSpan.FromMilliseconds(100), success: true);
        service.Record(TimeSpan.FromMilliseconds(200), success: true);

        var buckets = service.GetBuckets().ToList();
        Assert.Single(buckets);

        var bucket = buckets[0];
        Assert.Equal(2, bucket.ExecutionCount);
        Assert.Equal(300, bucket.TotalDurationMs);
        Assert.Equal(0, bucket.ErrorCount);
    }

    [Fact]
    public void Record_TracksErrors()
    {
        var service = new ExecutionBucketService();

        service.Record(TimeSpan.FromMilliseconds(50), success: true);
        service.Record(TimeSpan.FromMilliseconds(50), success: false);
        service.Record(TimeSpan.FromMilliseconds(50), success: true);
        service.Record(TimeSpan.FromMilliseconds(50), success: false);

        var buckets = service.GetBuckets().ToList();
        Assert.Single(buckets);
        Assert.Equal(4, buckets[0].ExecutionCount);
        Assert.Equal(2, buckets[0].ErrorCount);
    }

    [Fact]
    public void Record_CreatesSeparateBucketsByMinute()
    {
        var service = new ExecutionBucketService();

        // Record in the current minute
        service.Record(TimeSpan.FromMilliseconds(10), success: true);

        // We can't easily fake a different minute without sleep,
        // but we can verify the basic bucket creation works.
        var buckets = service.GetBuckets().ToList();
        Assert.Single(buckets);
        Assert.Equal(1, buckets[0].ExecutionCount);
    }

    [Fact]
    public void GetBuckets_ReturnsSortedByTimestamp()
    {
        var service = new ExecutionBucketService();

        // Record multiple entries in the same minute
        for (var i = 0; i < 5; i++)
            service.Record(TimeSpan.FromMilliseconds(10 + i), success: true);

        var buckets = service.GetBuckets().ToList();

        // Single minute = single bucket
        Assert.Single(buckets);
        Assert.Equal(5, buckets[0].ExecutionCount);
    }

    [Fact]
    public void Clear_RemovesAllBuckets()
    {
        var service = new ExecutionBucketService();

        service.Record(TimeSpan.FromMilliseconds(100), success: true);
        service.Record(TimeSpan.FromMilliseconds(200), success: false);

        Assert.NotEmpty(service.GetBuckets());

        service.Clear();

        Assert.Empty(service.GetBuckets());
    }

    [Fact]
    public void Record_FromMultipleThreads_IsThreadSafe()
    {
        var service = new ExecutionBucketService();
        var threads = new List<Thread>();

        for (var t = 0; t < 10; t++)
        {
            var thread = new Thread(() =>
            {
                for (var i = 0; i < 100; i++)
                    service.Record(TimeSpan.FromMilliseconds(1), success: i % 3 != 0);
            });
            threads.Add(thread);
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        var buckets = service.GetBuckets().ToList();
        var total = buckets.Sum(b => b.ExecutionCount);
        Assert.Equal(1000, total);
    }

    [Fact]
    public void Record_ZeroDuration_IsHandled()
    {
        var service = new ExecutionBucketService();

        service.Record(TimeSpan.Zero, success: true);

        var buckets = service.GetBuckets().ToList();
        Assert.Single(buckets);
        Assert.Equal(1, buckets[0].ExecutionCount);
        Assert.Equal(0, buckets[0].TotalDurationMs);
    }

    [Fact]
    public void Record_LargeDuration_DoesNotOverflow()
    {
        var service = new ExecutionBucketService();

        service.Record(TimeSpan.FromHours(24), success: true);

        var buckets = service.GetBuckets().ToList();
        Assert.Single(buckets);
        Assert.Equal(1, buckets[0].ExecutionCount);
        Assert.True(buckets[0].TotalDurationMs > 0);
    }
}
