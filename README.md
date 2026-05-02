# QuartzDashboard v1.0

A beautiful, self-contained **Quartz.NET scheduler dashboard** — drop it into any ASP.NET Core app with two lines of code.

![Dark UI](https://img.shields.io/badge/UI-Dark_Alpine.js_Tailwind-6366f1)
![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4)
![NuGet](https://img.shields.io/badge/NuGet-N8.QuartzDashboard-004880)

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work
- **Control** the scheduler — start, standby, trigger jobs, pause/resume jobs and triggers
- **Track** execution history with per-minute bucketed stats and live SVG charts
- **Monitor** execution rate, average duration, and error trends in real time
- **Zero build step** — single HTML SPA with Alpine.js + Tailwind CDN

## Quick Start

```bash
dotnet add package N8.QuartzDashboard
```

```csharp
// Program.cs
using QuartzDashboard;

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();

builder.Services.AddQuartzDashboard();
// Optional: track fire history with per-minute statistics
builder.Services.AddQuartzDashboardHistory();

var app = builder.Build();
app.UseRouting();
app.UseQuartzDashboard();

app.Run();
```

Open **`/quartz`** in your browser.

## Dashboard Pages

| Page | What you see |
|------|-------------|
| **Overview** | Scheduler info + stat cards with SVG sparkline execution trends |
| **Jobs** | All jobs with inline trigger details, live search/filter, trigger/pause/resume |
| **Triggers** | Grouped by job (accordion), schedule descriptions, relative fire times |
| **Executing** | Currently running jobs with animated duration bars |
| **History** | Last 100 fire events with relative duration bars, job filter |
| **Graph** | Dual-line SVG chart: execution count + avg duration, zoom toggles |
| **Settings** | Refresh interval slider, per-page auto-refresh toggles |

Auto-refreshes every 5 seconds. Dark theme, responsive, collapsible sidebar.

## API Endpoints

All endpoints under `{basePath}/api/` (default: `/quartz/api/`).

### Scheduler

| Method | Path | Description |
|--------|------|-------------|
| GET | `/scheduler` | Metadata, status, uptime, version |
| POST | `/scheduler/start` | Start / resume from standby |
| POST | `/scheduler/standby` | Pause scheduler |

### Jobs

| Method | Path | Description |
|--------|------|-------------|
| GET | `/jobs` | All jobs with triggers + schedule descriptions |
| GET | `/jobs/{group}/{name}` | Single job detail with JobDataMap |
| POST | `/jobs/{group}/{name}/trigger` | Fire job immediately |
| POST | `/jobs/{group}/{name}/pause` | Pause job |
| POST | `/jobs/{group}/{name}/resume` | Resume job |

### Triggers

| Method | Path | Description |
|--------|------|-------------|
| GET | `/triggers` | All triggers with schedule descriptions |
| GET | `/triggers/{group}/{name}` | Single trigger detail |
| POST | `/triggers/{group}/{name}/pause` | Pause trigger |
| POST | `/triggers/{group}/{name}/resume` | Resume trigger |

### Runtime

| Method | Path | Description |
|--------|------|-------------|
| GET | `/executing` | Currently executing jobs with duration |
| GET | `/history` | Last 100 fire events (requires `AddQuartzDashboardHistory()`) |
| **GET** | **`/stats`** | **Execution buckets (per-minute), rate, avg duration — for the graph** |

### Stats Response

```json
{
  "totalExecutions": 145,
  "uptimeMinutes": 34.2,
  "executionRate": 4.0,
  "executionBuckets": [
    { "minute": "19:05", "count": 4, "avgDurationMs": 850.5, "errorRate": 0.0 },
    { "minute": "19:06", "count": 2, "avgDurationMs": 1200.3, "errorRate": 0.0 }
  ]
}
```

## Configuration

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/admin/scheduler";  // default: "/quartz"
});
```

## Architecture

```
Request → app.Map("/quartz", branch)
          ├── /api/*       → HandleApi (route by path segments)
          ├── /index.html  → Serve embedded SPA (Alpine.js + Tailwind)
          └── anything else → SPA fallback (index.html)
```

- **Backend**: Raw ASP.NET Core middleware using `app.Map()` — zero routing conflicts
- **Frontend**: Single HTML file (~65KB) with Alpine.js 3.x + Tailwind CSS v4 CDN
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `Quartz` 3.18.0, `Quartz.Extensions.DependencyInjection` 3.18.0

## Demo

```bash
cd QuartzDashboard.Demo
dotnet run
# Open http://localhost:5190/quartz
```

The demo registers 5 jobs with different schedules:
- **HealthCheck** — every 15s (fast, generates frequent graph data)
- **CacheWarmup** — every 30s (variable 1-3s duration)
- **ReportGeneration** — every 2min (long 4-6s duration, visible spikes)
- **DataSync** — CRON `0/30 * * * * ?` (fires at :00 and :30)
- **ManualNotification** — durable, fire from the dashboard UI

## History & Stats

`builder.Services.AddQuartzDashboardHistory()` registers an `IJobListener` via an `IHostedService` that:

1. Records the last **100 fire events** in a `ConcurrentQueue<FireRecord>`
2. Buckets executions **per-minute** into 120 rolling `ExecutionBucket` entries
3. Tracks per-bucket: count, total duration, error count
4. Powers the `/api/stats` endpoint and the SVG execution graph

No external storage — all data is in-memory, ~7KB for 120 buckets.

## Changelog

### v1.0.0 (2026-05-02)
- Complete UI/UX overhaul: glassmorphism, collapsible sidebar, animations, responsive
- Live execution graph: SVG dual-line chart with zoom toggles and tooltips
- New `/api/stats` endpoint with per-minute execution buckets
- Schedule descriptions on triggers ("Every 00:00:30", CRON expressions)
- Expandable job rows with inline trigger details
- Live search/filter on jobs and history pages
- Color-coded job borders by state
- Settings panel: refresh interval, per-page auto-refresh
- 5 demo jobs with diverse schedules

### v0.3.0 (2026-05-02)
- Fixed routing via `app.Map()` for Blazor WASM compatibility

### v0.2.0 (2026-05-02)
- Raw middleware approach, all endpoints verified

## License

MIT — use it, ship it, open-source it.
