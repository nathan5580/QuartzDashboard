using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Standard pagination envelope returned by the list-style endpoints (<c>/api/jobs</c>,
/// <c>/api/triggers</c>, <c>/api/history</c>). Wire format: <c>{ data, total, offset, limit }</c>.
/// </summary>
/// <typeparam name="T">The element type of the data page.</typeparam>
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, int Total, int Offset, int Limit);

/// <summary>
/// Standard confirmation envelope for state-changing operations (pause, resume, trigger, etc.).
/// Nullable members are omitted from the JSON payload when not set so the wire format matches
/// the previous shape (e.g. <c>{"status":"paused"}</c> rather than <c>{"status":"paused","job":null,...}</c>).
/// </summary>
public sealed record StatusResponse(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Job = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Group = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Trigger = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Calendar = null);

/// <summary>
/// Standard error envelope.
/// </summary>
public sealed record ErrorResponse(string Error);

/// <summary>
/// Single fire-history row as returned by <c>/api/history</c> and <c>/api/timeline</c>.
/// </summary>
public sealed record FireRecordDto(
    string JobKey,
    string TriggerKey,
    DateTimeOffset FireTime,
    double Duration,
    bool Success,
    int RefireCount,
    double RelativeTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExceptionMessage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExceptionType = null);
