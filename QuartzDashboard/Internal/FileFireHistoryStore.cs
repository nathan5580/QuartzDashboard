using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Internal;

/// <summary>
/// File-backed fire history store. Reads history from disk on startup; persists writes
/// in coalesced batches so a busy scheduler doesn't issue one disk write per fire.
/// Uses System.Text.Json — no extra NuGet dependencies required.
/// </summary>
internal sealed class FileFireHistoryStore : IFireHistoryStore, IDisposable
{
    private readonly InMemoryFireHistoryStore _inner;
    private readonly string _filePath;
    private readonly ILogger<FileFireHistoryStore> _logger;
    private readonly object _writeLock = new();
    private readonly Timer _flushTimer;
    private int _pendingWrites;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions PersistOptions = new() { WriteIndented = false };

    public FileFireHistoryStore(string filePath, ILogger<FileFireHistoryStore> logger, int maxRecords = 500, int retentionHours = 24)
    {
        _filePath = filePath;
        _logger = logger;
        _inner = new InMemoryFireHistoryStore(maxRecords, retentionHours);

        // Coalesce writes: any number of RecordFire calls inside FlushInterval result in
        // a single disk write. Trade-off: up to FlushInterval of recent records lost on
        // unclean shutdown. Dispose() flushes synchronously to bound this on graceful exit.
        _flushTimer = new Timer(_ => FlushIfPending(), null, Timeout.Infinite, Timeout.Infinite);
        _inner.OnFireRecorded += _ => ScheduleFlush();

        LoadFromDisk();
    }

    public int Count => _inner.Count;
    public event Action<FireRecord>? OnFireRecorded
    {
        add => _inner.OnFireRecorded += value;
        remove => _inner.OnFireRecorded -= value;
    }

    public void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null)
        => _inner.RecordFire(jobKey, triggerKey, fireTime, duration, success, refireCount, exceptionMessage, exceptionType);

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
        => _inner.GetRecent(count, offset);

    public void Clear()
    {
        _inner.Clear();
        ScheduleFlush();
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        FlushIfPending();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var records = JsonSerializer.Deserialize<List<FireRecord>>(json);
            if (records?.Count > 0)
                _inner.Seed(records);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load fire history from {Path} — starting fresh", _filePath);
        }
    }

    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _pendingWrites, 1) == 0)
            _flushTimer.Change(FlushInterval, Timeout.InfiniteTimeSpan);
    }

    private void FlushIfPending()
    {
        if (Interlocked.Exchange(ref _pendingWrites, 0) == 0)
            return;

        try
        {
            lock (_writeLock)
            {
                var records = _inner.GetRecent(int.MaxValue).ToList();
                var json = JsonSerializer.Serialize(records, PersistOptions);
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_filePath, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist fire history to {Path}", _filePath);
        }
    }
}
