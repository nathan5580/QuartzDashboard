# QuartzDashboard v2.0

A beautiful, self-contained **Quartz.NET scheduler dashboard** — drop it into any ASP.NET Core app with two lines of code.

![Dark UI](https://img.shields.io/badge/UI-Dark_Alpine.js_Tailwind-6366f1)
![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4)
![NuGet](https://img.shields.io/badge/NuGet-Dot.QuartzDashboard-004880)

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work
- **Control** the scheduler — start, standby, trigger jobs, pause/resume jobs and triggers
- **Track** execution history with per-minute bucketed stats and live SVG charts
- **Monitor** execution rate, average duration, and error trends in real time
- **Secure** your dashboard with authentication, role-based access, and authorization policies
- **Zero build step** — single HTML SPA with Alpine.js + Tailwind CDN

## Quick Start

```bash
dotnet add package Dot.QuartzDashboard
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
| **Timeline** | Color-coded execution dots with tooltips, real-time now-line |
| **Health** | Success rate, failed executions, pool utilization, scheduler diagnostics, failure list |
| **Calendars** | Quartz calendars list with type badges and description |
| **Settings** | Refresh interval slider, per-page auto-refresh toggles, data management |

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
    options.ReadOnly = false;           // disable trigger/start/stop buttons
    options.UseSignalR = true;          // real-time updates via SignalR

    // --- New in v2.0 ---

    options.Enabled = true;             // set to false to completely disable the dashboard
                                        // (UseQuartzDashboard() becomes a no-op)

    options.RequireAuthentication = true;  // require authenticated users
    options.AllowedRoles = ["Admin"];       // restrict to specific roles
    options.RequiredPolicy = "CanViewDashboard";  // or use a named authorization policy

    options.MaxFireHistory = 100;        // max fire history records (default: 100)
    options.MaxExecutionLogsPerJob = 50; // max execution log entries per job (default: 50)
});
```

### Feature Gating with `Enabled`

When `Enabled` is set to `false`, the dashboard is completely disabled at the middleware level.
`UseQuartzDashboard()` becomes a no-op — no routes are registered, no resources used.

Useful for feature flags or environment-based gating:

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Enabled = IsProduction || featureFlags.IsDashboardEnabled;
});
```

### Authentication & Authorization

Three levels of access control (checked in order):

1. **`RequireAuthentication`** — unauthenticated requests get 401
2. **`RequiredPolicy`** — if set, uses `IAuthorizationService` to check a named policy (403 on failure)
3. **`AllowedRoles`** — if set (and no policy), user must be in one of the listed roles (403 on failure)

```csharp
// Example: only users with the "Admin" role can access
builder.Services.AddQuartzDashboard(options =>
{
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Admin"];
});

// Example: use a custom authorization policy
builder.Services.AddQuartzDashboard(options =>
{
    options.RequireAuthentication = true;
    options.RequiredPolicy = "RequireDashboardAccess";
});

// With ASP.NET Core auth configured
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireDashboardAccess", policy =>
        policy.RequireRole("Admin", "Operator"));
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
- **Strong-named**: Assembly is signed for GAC/strong-name scenarios

## Demo

```bash
cd QuartzDashboard.Demo

# Run with default settings (port 5190)
dotnet run

# Run on a custom port
dotnet run -- -p 8080

# Enable authentication mode (requires cookie auth — useful for testing access control)
dotnet run -- --auth

# Enable read-only mode (disables trigger/start/stop/delete actions)
dotnet run -- --readonly

# Combine flags
dotnet run -- -p 5000 --auth --readonly
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

The history buffer size and per-job log limits can be configured via `QuartzDashboardOptions`:

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.MaxFireHistory = 200;       // keep up to 200 fire records
    options.MaxExecutionLogsPerJob = 100; // keep up to 100 log lines per job
});
```

## Changelog

### v2.0.0 (2026-05-03)
- Breaking: `QuartzDashboardOptions` now has: `Enabled`, `RequireAuthentication`, `AllowedRoles`, `RequiredPolicy`, `MaxFireHistory`, `MaxExecutionLogsPerJob`
- `UseQuartzDashboard()` is a no-op when `Enabled=false`
- Authentication support with role-based and policy-based authorization
- Strong-named assembly for GAC/enterprise scenarios
- Package icon and SourceLink support
- New demo CLI flags: `-p` for port, `--auth` for auth mode, `--readonly` for read-only mode

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
