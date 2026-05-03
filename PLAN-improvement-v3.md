# QuartzDashboard v3 — Improvement Plan

## Current State (v2.0.0 — Audited May 3, 2026)

| Metric | Value |
|--------|-------|
| Backend | 1,049 lines (ApplicationBuilderExtensions.cs alone) |
| Frontend | 4,074 lines (single index.html SPA) |
| Rendering | PASS — all pages load correctly, no JS errors |
| API | 25+ endpoints, all functional |
| SignalR | Works — real-time push for job executions |
| Build | 0 errors, 0 warnings across net8.0/net9.0/net10.0 |
| Published | NuGet.org — Dot.QuartzDashboard v2.0.0 |

---

## Phase A: Code Quality & Architecture (Backend)

### A1. Split ApplicationBuilderExtensions.cs into focused files

**Problem:** 1,049-line file with 25 handler methods, request types, and static state mixed together.

**Plan:**

```csharp
// New file structure:
/QuartzDashboard/
├── Middleware/
│   ├── QuartzDashboardMiddleware.cs       # The Map() branch + routing
│   ├── QuartzDashboardAuthMiddleware.cs   # Auth logic extracted
│   └── QuartzDashboardStaticFilesMiddleware.cs  # Static file serving
├── Handlers/
│   ├── SchedulerHandlers.cs              # GetSchedulerInfo, Start, Standby
│   ├── JobHandlers.cs                    # CRUD + batch ops for jobs
│   ├── TriggerHandlers.cs                # CRUD for triggers
│   ├── HistoryHandlers.cs                # Fire history + timeline + stats
│   ├── CalendarHandlers.cs               # Calendar CRUD
│   └── ConfigHandlers.cs                 # /api/config
├── Models/                               # (keep existing records, move to separate files)
│   ├── CreateJobRequest.cs
│   ├── CreateTriggerRequest.cs
│   ├── CreateCalendarRequest.cs
│   ├── BatchJobRequest.cs
│   ├── UpdateJobRequest.cs
│   └── ExecutionBucket.cs
├── Services/
│   ├── ExecutionBucketService.cs          # Execution bucket logic from static method
│   └── PlaceholderJob.cs                 # (move from bottom of big file)
└── QuartzDashboardApplicationBuilderExtensions.cs  # Keep: just the public API
```

**Why:** Maintainability. Each handler file is 30-60 lines. Centralized handlers can be unit-tested. No more 1KB scrolling.

### A2. Fix ExecutionBuckets thread-safety

**Problem:** `RecordExecution()` does `TryPeek` + `TryDequeue` + `Enqueue` as a non-atomic operation on `ConcurrentQueue`. Two concurrent calls can corrupt the bucket state.

**Fix:** Use `ConcurrentDictionary<DateTimeOffset, MutableBucket>` or a lock-free approach with `Interlocked`:

```csharp
internal sealed class ExecutionBucketService
{
    private readonly ConcurrentDictionary<long, Bucket> _buckets = new();
    private long _currentMinute;
    
    public void Record(TimeSpan duration, bool success)
    {
        var now = DateTimeOffset.UtcNow;
        var minute = now.Year * 100000000L + now.Month * 1000000L + now.Day * 10000L + now.Hour * 100L + now.Minute;
        
        var bucket = _buckets.GetOrAdd(minute, _ => new Bucket());
        Interlocked.Increment(ref bucket.ExecutionCount);
        Interlocked.Add(ref bucket.TotalDurationMs, (long)duration.TotalMilliseconds);
        if (!success) Interlocked.Increment(ref bucket.ErrorCount);
        
        // Cleanup old buckets
        if (minute != Interlocked.Read(ref _currentMinute))
        {
            Interlocked.Exchange(ref _currentMinute, minute);
            foreach (var key in _buckets.Keys.Where(k => k < minute - 120))
                _buckets.TryRemove(key, out _);
        }
    }
}
```

### A3. Eliminate N+1 query in GetAllJobs

**Problem:** `GetAllJobs()` calls `GetCurrentlyExecutingJobs()` inside the outer loop (line 332), meaning it fetches the full executing jobs list for EVERY job.

**Fix:** Fetch executing jobs once outside the loop and filter client-side:

```csharp
private static async Task<IResult> GetAllJobs(IScheduler sched, HttpContext ctx, QuartzDashboardOptions options)
{
    var offset = /* ... */;
    var limit = /* ... */;
    var executingJobs = await sched.GetCurrentlyExecutingJobs();
    var executingKeys = new HashSet<JobKey>(executingJobs.Select(j => j.JobDetail.Key));
    
    foreach (var group in groups)
    {
        foreach (var key in keys)
        {
            var isExecuting = executingKeys.Contains(key);  // O(1) lookup
            // ... rest
        }
    }
}
```

### A4. Deduplicate batch operations

**Problem:** 4 batch methods (pause/resume/trigger/delete) are identical except for the inner operation.

**Fix:** Single generic batch method:

```csharp
private static async Task<IResult> BatchJobOperation(IScheduler sched, BatchJobRequest? req, 
    QuartzDashboardOptions options, Func<IScheduler, JobKey, Task> operation)
{
    if (options.ReadOnly) return Results.Forbid();
    /* ... parse jobs, call operation for each ... */
}
```

### A5. Clean up request/response types

- Move `CreateJobRequest`, `CreateTriggerRequest`, etc. to their own files
- Use `Required` attribute on `Name` fields instead of null checks in handlers
- Add `[JsonSerializable(typeof(...))]` attributes for source-gen JSON support
- Add proper XML documentation on ALL public types

### A6. Use basePath consistently

**Problem:** `/quartz` is hardcoded in the SPA JavaScript. If someone configures a custom path, the SPA breaks.

**Fix:** The SPA already fetches `config.basePath` from `/api/config`. Use it everywhere instead of hardcoded `/quartz/api/...`. Replace all fetch URLs:

```javascript
api(path) { return this.config.basePath + '/api' + path; }
fetchJobs() { return this.fetchApi(this.api('/jobs')); }
```

Also fix SignalR hub URL to use basePath.

---

## Phase B: Frontend Stability & Code Quality

### B1. Fix init() race conditions

**Problems found:**
1. `splashVisible = false` called twice — immediate (line 1155) AND setTimeout (line 1152) — creates race
2. `$watch('settings.refreshInterval', ...)` has a copy-paste bug with leftover `splashVisible` call (lines 1158-1161)
3. Graph `$refs.graphContainer` check happens before Alpine resolves refs

**Fix:** Clean init():

```javascript
async init() {
    // 1. Load persisted settings
    this.loadSettings();
    
    // 2. Fetch config
    await this.fetchConfig();
    
    // 3. Setup keyboard shortcuts
    this.setupKeyboardShortcuts();
    
    // 4. Load initial data
    await Promise.all([
        this.refreshAll(),
        this.loadHistory(),
        this.loadStats(),
        this.connectSignalR(),
    ]);
    
    // 5. Hide splash after data OR timeout
    this.splashVisible = false;
    this.startAutoRefresh();
    this.setupWatchers();
    this.startTimelineTicker();
}
```

### B2. Remove hardcoded `/quartz` paths

**Problem:** Every fetch in the SPA hardcodes `/quartz/api/...`. If the user configures a custom path, everything breaks.

**Plan:**
- Replace all `fetch('/quartz/api/...')` with `fetch(this.config.basePath + '/api/...')`
- Replace SignalR URL with config-based
- Store basePath in the config response and use it globally
- Create a helper: `apiUrl(endpoint) => this.config.basePath + '/api' + endpoint`

### B3. Add loading and error states for all operations

**Missing states:**
- `loadMoreJobs()` has no error handling (line 3024)
- Page transitions show no loading spinner during fetch
- Batch operations have no loading indicator
- Delete confirmation doesn't show processing state

### B4. Extract graph sparkline generation into a reusable function

**Problem:** Inline SVG polyline generation is duplicated across sparkline (3 places), graph, and timeline.

**Fix:** Create a shared `SVGHelper` object:

```javascript
const SVGHelper = {
    linePoints(data, field, width, height, margin) { /* ... */ },
    areaPoints(data, field, width, height, margin) { /* ... */ },
    sparkline(data, field, width, height) { /* ... */ },
};
```

### B5. Fix the `durationBuckets` computed property

**Problem:** Duration buckets use `<10ms`, `10-50ms`, `50ms+` labels but the actual threshold values are `100ms` and `1000ms` — labels don't match thresholds.

### B6. Add font-display: swap for Google Fonts

**Problem:** The `@import url('...')` in CSS blocks rendering. If the Google Fonts CDN has a DNS failure (as seen in testing), the page doesn't get any font.

**Fix:** Add `&display=swap` parameter and a local font fallback:

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
```

Also add a CSS `font-family` fallback chain that works when Google Fonts is unreachable.

### B7. Fix trigger-grid CSS definition

The triggers page uses `class="trigger-grid"` but I don't see a corresponding CSS rule. Check that the grid layout is defined or add it.

---

## Phase C: UI/UX Polish (Make It "Useful As Fuck")

### C1. Global Search Bar (Cmd+K)

**Current:** Command palette exists but only searches nav items and jobs.

**Target:** Full keyboard-accessible global search that searches across:
- All jobs (by name, group, type, description)
- All triggers (by name, group, job name)
- Job data map values
- Navigation items
- Recent history entries with job names

```javascript
get globalSearchResults() {
    if (!this.globalSearchQuery) return [];
    const q = this.globalSearchQuery.toLowerCase();
    const results = [];
    
    // Search jobs
    for (const job of this.jobs) {
        if (job.name.toLowerCase().includes(q) || ...)
            results.push({ type: 'job', ... });
    }
    // Search triggers
    // Search history
    // etc.
    
    return results.sort(...).slice(0, 20);
}
```

### C2. Keyboard Shortcuts Sheet (? key)

**Current:** Keyboard shortcuts exist (Ctrl+J/K, Ctrl+N, ? opens palette, Escape) but no documentation.

**Target:** Press `?` to show a keyboard shortcuts overlay:

```
Keyboard Shortcuts
─────────────────
  Ctrl/Cmd + J     Previous page
  Ctrl/Cmd + K     Next page
  Ctrl/Cmd + N     Create job/trigger
  Ctrl/Cmd + P     Command palette
  Ctrl/Cmd + R     Refresh
  Ctrl/Cmd + ,     Settings
  ? / Ctrl+/       This help
  Escape           Close modal / palette
  ← →              Timeline zoom
```

### C3. Job Execution Timeline with Zoom/Pan

**Current:** Timeline shows last 60 seconds with hardcoded time range.

**Target:**
- Zoom in/out with mouse wheel (±)
- Click-drag to pan
- Time range selector: 30s / 1m / 5m / 15m
- Tooltip shows exact fire time + duration on hover
- Filter by job group with multi-select
- "Now" indicator line that updates every second

### C4. Graph Page Overhaul

**Current:** SVG-based graph with `polyline` rendering. Limited time ranges (5m/6h added but undocumented).

**Target:**
- **Three chart modes:** Line chart, Bar chart, Heatmap
- **Time range presets:** 5m, 15m, 30m, 1h, 6h, 24h, 7d, custom date picker
- **Zoomable:** Click-drag to zoom into a time region
- **Per-job breakdown:** Toggle individual job lines on/off with color legend
- **Threshold lines:** Configurable dashed lines for duration/error rate alerts
- **Export:** PNG download of current graph view
- **Stats summary row:** P50, P95, P99 latency across selected period

### C5. Scheduler Health Dashboard

**Current:** Overview page has 6 metric cards + scheduler details + activity feed.

**Target:** Add a dedicated health/status section:
- Last N failures with stack traces (from execution log)
- Memory/thread pool utilization gauge
- Fire history growth rate
- Trigger misfire count with trend
- Scheduler uptime percentage over last 24h
- Color-coded status for each major subsystem

### C6. Cron Expression Tester

**Current:** Users type cron expressions with no validation feedback.

**Target:** Inline cron tester in the Create Trigger modal:
- Validate cron syntax on keyup (green/red indicator)
- Show next 5 fire times on the fly
- "Test" button that simulates the trigger without scheduling it
- Presets include tooltip explanations ("Every 5 minutes: `0 */5 * * * ?`")

### C7. Job Detail Modal Enhancement

**Current:** Clicking a job row expands an inline section with triggers/logs tabs.

**Target:** Add a full-screen job detail modal:
- Full job metadata display (all Quartz properties)
- JobDataMap key-value editor with add/remove
- Execution history table specific to this job
- Trigger management (add/pause/resume/delete without leaving modal)
- JSON export of job definition
- "Clone job" button

### C8. Batch Operations UX

**Current:** Checkbox select + action bar.

**Target:**
- Shift+click for range selection
- "Select All" with page-aware behavior (current page vs. all pages)
- Confirm dialog with job count and names
- Progress indicator during batch operation with live status updates
- Undo last batch operation (restore via API)

### C9. Notifications & Alerts

**Current:** Toast messages only.

**Target:**
- Configurable alerts: "Notify me when any job fails" / "When X job runs longer than Y seconds"
- Browser notification API integration (permission-gated)
- Sound on job failure (optional, configurable)
- Badge count in browser tab title: `(3) Quartz Dashboard`

### C10. Mobile Responsiveness

**Current:** Basic mobile support with hamburger menu + responsive breakpoints.

**Target:**
- Touch-friendly modals (swipe to dismiss)
- Bottom navigation bar instead of sidebar on <768px
- Collapsible stat cards on mobile (show as accordion)
- Touch-optimized sparkline interactions
- Fixed header with page title + actions on scroll

### C11. Settings Persistence & Sync

**Current:** localStorage for theme, compact mode, default page, items per page.

**Target:**
- Add all settings to localStorage: theme, auto-refresh pages, date format, alert prefs
- Settings export/import (JSON file)
- Reset per-category (not just full reset)
- "Settings" indicator when non-default values are applied

### C12. Data Export

- Export fire history as CSV/JSON
- Export job definitions as JSON (for backup/restore)
- Export current graph as PNG
- Export dashboard state as shareable URL (if host app supports it)

---

## Phase D: Backend API Enhancements

### D1. API Versioning

Add `/api/v1/config`, `/api/v1/jobs`, etc. Keep backward compat with `/api/config` via redirect.

### D2. Add PATCH endpoint for partial job updates

Current PUT replaces everything. Add PATCH for targeted JobDataMap updates.

### D3. Add job execution history persistence option

Current in-memory only. Add `UseSqlServerHistoryStore(connectionString)` option that persists fire history to SQL Server/PostgreSQL.

### D4. Add webhook support

Let users register webhook URLs that get POSTed on job events:

```csharp
options.Webhooks = new()
{
    OnJobFailure = "https://hooks.slack.com/...",
    OnJobSlow = "https://...",  // When duration > threshold
};
```

### D5. Add rate limiting to API

Prevent accidental DoS from rapid refresh/trigger calls.

### D6. Add API key auth option

In addition to ASP.NET Core auth, allow a simple API key header for programmatic access.

---

## Phase E: Performance & Delivery

### E1. Lazy-load page templates

**Problem:** All 9 page templates are in the initial HTML payload (210KB).

**Fix:** Split into separate HTML fragments loaded on demand:

```javascript
async loadPageTemplate(pageId) {
    const resp = await fetch(`${this.config.basePath}/pages/${pageId}.html`);
    this[`${pageId}Template`] = await resp.text();
}
```

Or use `<template>` tags that are lazily populated.

### E2. Add response caching headers

- Add ETag support to job/trigger list endpoints
- Add `304 Not Modified` responses when data hasn't changed
- Use conditional GETs in the SPA

### E3. Optimize SignalR message batching

Instead of sending individual `jobExecuted` messages, batch events every 100ms:

```csharp
// In DashboardSignalRBridge
private readonly Channel<object> _eventChannel = Channel.CreateBounded<object>(100);

// Producer
eventBus.OnJobExecuted += e => _eventChannel.Writer.TryWrite(e);

// Consumer (batches every 100ms)
while (await _eventChannel.Reader.WaitToReadAsync())
{
    var batch = new List<object>();
    while (_eventChannel.Reader.TryRead(out var item) && batch.Count < 20)
        batch.Add(item);
    
    await hubContext.Clients.Group(GroupName).SendAsync("jobExecutedBatch", batch);
}
```

---

## Implementation Priority

| Priority | Phase | Items | Effort | Impact |
|----------|-------|-------|--------|--------|
| P0 | B1, B2, B5, B7 | Race conditions, hardcoded paths, CSS | 0.5d | High — prevents bugs |
| P0 | A1, A2, A3 | File split, thread safety, N+1 | 1d | High — maintainability + correctness |
| P1 | C1, C2, C6 | Global search, shortcuts, cron tester | 1.5d | High — UX delight |
| P1 | C3, C4 | Timeline zoom, graph overhaul | 2d | High — data visualization |
| P2 | A4, A5, A6 | Dedup, cleanup, basePath | 1d | Medium — code quality |
| P2 | C5, C7 | Health dashboard, job detail modal | 1.5d | Medium — features |
| P2 | D1-D4 | API versioning, webhooks, persistence | 2d | Medium — extensibility |
| P3 | B3, B4, B6 | Loading states, SVG, fonts | 0.5d | Low — polish |
| P3 | C8-C12 | Batch UX, alerts, mobile, settings | 3d | Low — nice-to-have |
| P3 | E1-E3 | Performance | 1d | Low — optimization |

---

## Rendering Audit Summary

| Page | State | Issues Found |
|------|-------|-------------|
| Overview | PASS | None — 6 stat cards, proper data, sparklines render |
| Jobs | PASS | Table with sort, filter, group filter, expand rows work |
| Triggers | PASS | Card grid layout, badges, state display |
| Executing | PASS | Gauge bar, duration bars, abort button |
| History | PASS | Stats bar, filter, relatime time display |
| Graph | PASS | SVG polyline rendering, time range selector |
| Timeline | PASS | Color-coded dots, tooltips |
| Calendars | PASS | Card list, expand detail, type badges |
| Settings | PASS | Slider, toggles, selects, data management |
| Modals | PASS | Create job, create trigger, delete confirm all work |
| SignalR | PASS | Hub connects, events push in real-time |
| Light Mode | PASS | Full light theme override CSS |
| Compact Mode | PASS | Padding/font-size overrides work |
| Responsive | PASS* | Basic breakpoints, mobile sidebar (needs C10 polish) |

**No JavaScript console errors during normal operation.** The only error during testing was `net::ERR_NAME_NOT_RESOLVED` from Google Fonts CDN — categorized as B6 fix.

---

## File creation/change summary

Expected new/modified files for P0-P1:

```
QuartzDashboard/
├── Middleware/
│   ├── QuartzDashboardMiddleware.cs           # NEW — route logic from big file
│   └── QuartzDashboardAuthMiddleware.cs       # NEW — auth checks
├── Handlers/
│   ├── SchedulerHandlers.cs                   # NEW
│   ├── JobHandlers.cs                         # NEW
│   ├── TriggerHandlers.cs                     # NEW
│   ├── HistoryHandlers.cs                     # NEW
│   ├── CalendarHandlers.cs                    # NEW
│   └── ConfigHandlers.cs                      # NEW
├── Services/
│   └── ExecutionBucketService.cs              # NEW — thread-safe buckets
├── Models/
│   ├── CreateJobRequest.cs                    # NEW (from bottom of big file)
│   ├── CreateTriggerRequest.cs                # NEW
│   ├── BatchJobRequest.cs                     # NEW
│   ├── CreateCalendarRequest.cs               # NEW
│   ├── UpdateJobRequest.cs                    # NEW
│   └── ExecutionBucket.cs                     # NEW
├── QuartzDashboardApplicationBuilderExtensions.cs  # MODIFIED — slim to ~40 lines
├── QuartzDashboardServiceCollectionExtensions.cs    # UNCHANGED
├── QuartzDashboardOptions.cs                        # UNCHANGED
├── Internal/
│   ├── QuartzDashboardOptions.cs             # DELETE — duplicate of main options file?
│   ├── DashboardEventBus.cs                  # UNCHANGED
│   ├── DashboardSchedulerListener.cs         # UNCHANGED
│   ├── IFireHistoryStore.cs                  # UNCHANGED
│   └── ExecutionLogBuffer.cs                 # UNCHANGED
├── SignalR/
│   └── QuartzDashboardHub.cs                 # MODIFIED — batch events (E3)
└── wwwroot/
    └── index.html                            # MODIFIED — P0-P1 SPA fixes
```
