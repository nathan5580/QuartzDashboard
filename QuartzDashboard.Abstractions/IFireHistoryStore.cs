namespace QuartzDashboard.Abstractions;

/// <summary>
/// Defines storage operations for persisting and querying recent Quartz job execution history.
/// Implement this interface to back the dashboard with a custom store (Postgres, Redis, etc.).
/// Default interface methods provide fallback implementations for the filter-pushdown overloads;
/// override them when your storage layer supports predicate pushdown natively.
/// </summary>
public interface IFireHistoryStore
{
    /// <summary>
    /// Records a completed job execution.
    /// </summary>
    /// <param name="jobKey">The fully qualified Quartz job key in <c>group.name</c> format.</param>
    /// <param name="triggerKey">The fully qualified Quartz trigger key in <c>group.name</c> format.</param>
    /// <param name="fireTime">The UTC time when the execution started.</param>
    /// <param name="duration">The total execution duration.</param>
    /// <param name="success"><see langword="true"/> when the execution completed without an exception.</param>
    /// <param name="refireCount">The Quartz refire count for the execution.</param>
    /// <param name="exceptionMessage">The exception message captured for a failed execution, if any.</param>
    /// <param name="exceptionType">The exception type captured for a failed execution, if any.</param>
    void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null);

    /// <summary>
    /// Gets the number of records currently stored.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets recent execution records in reverse chronological order.
    /// </summary>
    /// <param name="count">The maximum number of records to return.</param>
    /// <param name="offset">The number of most recent records to skip.</param>
    /// <returns>A sequence of recent <see cref="FireRecord"/> entries.</returns>
    IEnumerable<FireRecord> GetRecent(int count, int offset = 0);

    /// <summary>
    /// Gets recent execution records optionally filtered to a single job key.
    /// Implementations should push the filter down to storage when possible.
    /// </summary>
    /// <param name="count">The maximum number of records to return.</param>
    /// <param name="offset">The number of most recent records to skip after filtering.</param>
    /// <param name="jobKeyFilter">When non-empty, only records whose <see cref="FireRecord.JobKey"/> matches (case-insensitively) are returned.</param>
    /// <returns>A sequence of recent <see cref="FireRecord"/> entries.</returns>
    IEnumerable<FireRecord> GetRecent(int count, int offset, string? jobKeyFilter)
    {
        if (string.IsNullOrWhiteSpace(jobKeyFilter))
            return GetRecent(count, offset);

        return GetRecent(int.MaxValue, 0)
            .Where(r => string.Equals(r.JobKey, jobKeyFilter, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(0, offset))
            .Take(Math.Max(0, count));
    }

    /// <summary>
    /// Gets the number of records currently stored that match the given filter.
    /// </summary>
    /// <param name="jobKeyFilter">When non-empty, counts only records whose <see cref="FireRecord.JobKey"/> matches (case-insensitively).</param>
    int CountFiltered(string? jobKeyFilter)
    {
        if (string.IsNullOrWhiteSpace(jobKeyFilter))
            return Count;

        return GetRecent(int.MaxValue, 0)
            .Count(r => string.Equals(r.JobKey, jobKeyFilter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes all stored execution records.
    /// </summary>
    void Clear();

    /// <summary>
    /// Occurs when a new execution record is stored.
    /// </summary>
    event Action<FireRecord>? OnFireRecorded;
}
