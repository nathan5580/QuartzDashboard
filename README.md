# Dot.QuartzDashboard

A beautiful, self-contained **Quartz.NET scheduler dashboard** — drop it into any ASP.NET Core app with two lines of code.

[![NuGet](https://img.shields.io/nuget/v/Dot.QuartzDashboard?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/Dot.QuartzDashboard)
[![Downloads](https://img.shields.io/nuget/dt/Dot.QuartzDashboard?style=flat-square&logo=nuget&color=green)](https://www.nuget.org/packages/Dot.QuartzDashboard)
[![Build](https://img.shields.io/github/actions/workflow/status/nathan5580/QuartzDashboard/dotnet.yml?branch=main&style=flat-square&logo=github)](https://github.com/nathan5580/QuartzDashboard/actions)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://opensource.org/licenses/MIT)

---

## What's New in v3.0.0

- Dark mode with automatic system preference detection
- SQLite-backed persistent history via `PersistHistoryToSqlite`
- Next-N-fires trigger preview for upcoming schedules
- CSV export from the History page
- Favicon failure badge for at-a-glance job health
- Faster job search/filter in the Jobs page
- ~50% smaller package thanks to bundled/minified embedded assets
- Breaking changes:
  - `AddQuartzDashboardHistory()` is no longer needed
  - `UseSystemFonts` was removed because system fonts are now the default

See [Migrating from v2.x to v3.0.0](#migrating-from-v2x-to-v300).

## 🤖 AI Prompt — Copy This Into Any Copilot/AI Assistant

> Use the block below to give any AI coding assistant instant, complete knowledge of this package.

```
You are integrating Dot.QuartzDashboard (NuGet) into an ASP.NET Core app.

PACKAGE: Dot.QuartzDashboard
TARGETS: net8.0, net9.0, net10.0
NAMESPACE: QuartzDashboard
DASHBOARD URL: /quartz (or options.Path)

--- MINIMUM SETUP (2 lines) ---

// 1. In DI (Program.cs or ServiceExtensions):
builder.Services.AddQuartzDashboard();

// 2. In middleware pipeline (before MapControllers and MapFallbackToFile):
app.UseQuartzDashboard();

That's it. Open /quartz in the browser.

--- FULL OPTIONS ---

builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/quartz";               // dashboard route prefix (default: "/quartz")
    options.Enabled = true;                  // false = UseQuartzDashboard() is a no-op
    options.ReadOnly = false;                // disable trigger/start/stop/delete actions
    options.UseSignalR = true;               // real-time push updates via SignalR

    // Auth (checked in order: auth → policy → roles)
    options.RequireAuthentication = false;   // require authenticated user (401 if not)
    options.RequiredPolicy = "";             // named IAuthorizationService policy (403 if fails)
    options.AllowedRoles = [];               // role whitelist — checked if no policy set (403 if fails)

    // History limits
    options.MaxFireHistory = 500;            // max fire records in memory (default: 500)
    options.MaxExecutionLogsPerJob = 50;     // max log lines per job
    options.HistoryRetentionHours = 24;      // auto-prune records older than this (0 = keep all)
    options.Title = "My App Dashboard";      // custom title in sidebar + browser tab

    // Persistence (survive restarts)
    options.PersistHistoryToSqlite = "quartz-history.db"; // persist history to SQLite
    options.PersistHistoryPath = "quartz-history.json";   // optional JSON fallback when SQLite is not used

    // Callbacks
    options.OnJobFailed = async (jobKey, ex) => { /* Slack/PagerDuty alert */ };
    options.WebhookUrl = "https://hooks.slack.com/...";  // POST JSON on job failure
});

--- APPSETTINGS BINDING (bind a config section directly) ---

// In appsettings.json:
{
  "QuartzDashboard": {
    "Enabled": true,
    "Path": "/quartz",
    "ReadOnly": false,
    "UseSignalR": true,
    "RequireAuthentication": false,
    "RequiredPolicy": "",
    "AllowedRoles": [],
    "MaxFireHistory": 500,
    "MaxExecutionLogsPerJob": 50
  }
}

// In code:
builder.Services.AddQuartzDashboard(options =>
    builder.Configuration.GetSection("QuartzDashboard").Bind(options));

--- ENVIRONMENT GATING ---

builder.Services.AddQuartzDashboard(options =>
{
    options.Enabled = !builder.Environment.IsProduction();
});

--- MIDDLEWARE ORDER RULES ---
- UseQuartzDashboard() must come BEFORE app.MapControllers() and app.MapFallbackToFile()
- If using auth, app.UseAuthentication() and app.UseAuthorization() must come BEFORE UseQuartzDashboard()
- UseSignalR = true makes the NuGet register its own SignalR hub — do NOT manually call app.MapHub<QuartzDashboardHub>()
- The NuGet handles /quartz → /quartz/ redirect automatically

--- API ENDPOINTS (all under {Path}/api/) ---
GET  /scheduler           - scheduler metadata, status, uptime
POST /scheduler/start     - start or resume from standby
POST /scheduler/standby   - put scheduler in standby
GET  /jobs                - all jobs with triggers (?offset=0&limit=50)
GET  /jobs/{group}/{name} - single job detail with JobDataMap
POST /jobs/{group}/{name}/trigger  - fire job immediately
POST /jobs/{group}/{name}/pause    - pause job
POST /jobs/{group}/{name}/resume   - resume job
POST /jobs/{group}/{name}/interrupt - interrupt executing job
DELETE /jobs/{group}/{name}        - delete job
GET  /triggers            - all triggers (?offset=0&limit=50)
POST /triggers/{group}/{name}/pause   - pause trigger
POST /triggers/{group}/{name}/resume  - resume trigger
GET  /executing           - currently running jobs with duration
GET  /history             - paginated fire events (?offset=0&limit=50)
GET  /stats               - per-minute execution buckets + rates + P50/P95/P99
GET  /stats/history       - rolling history for the graph
GET  /health              - scheduler health, success rate, thread pool, failure list
GET  /timeline            - execution timeline data (up to 500 records)
GET  /calendars           - calendar list
GET  /config              - dashboard config (readonly flag etc.)

--- SIGNALR HUB ---
Hub class: QuartzDashboard.QuartzDashboardHub
Default endpoint: {Path}/hub  (e.g. /quartz/hub)
Registered automatically when UseSignalR = true — no manual MapHub needed.
POST {Path}/hub/negotiate?negotiateVersion=1  → 200 when working

--- COMMON MISTAKES ---
- Do NOT call app.MapHub<QuartzDashboardHub>() yourself when UseSignalR = true
- Do NOT place UseQuartzDashboard() after MapFallbackToFile — Blazor WASM will swallow all /quartz routes
- Visiting /quartz (no trailing slash) works — NuGet redirects to /quartz/ automatically
- Dashboard assets are embedded in the package — no external CDN allowlist is required
```

---

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work
- **Control** the scheduler — start, standby, trigger jobs, pause/resume/delete jobs and triggers
- **Track** execution history with server-side pagination, CSV export, per-minute bucketed stats, and live SVG charts
- **Preview** the next scheduled fire times with next-N-fires trigger inspection
- **Monitor** execution rate, average duration, P50/P95/P99 percentiles, and error trends in real time
- **Search** jobs quickly with inline filters, plus global search across jobs, triggers, and history with `Ctrl+K`
- **Navigate** with keyboard shortcuts — press `?` to see all
- **Inspect** execution details — click any history row for full stacktraces and metadata
- **Build** CRON expressions visually with the built-in builder and presets
- **Embed** the dashboard in iframes with `?embed=true` (strips sidebar/header)
- **Secure** your dashboard with authentication, role-based access, and authorization policies
- **Persist** fire history to SQLite, JSON, or in-memory storage depending on your needs
- **Adapt** automatically to dark or light mode using the system theme
- **Alert** on job failures via callbacks, webhooks, or the favicon failure badge
- **Stay self-contained** — bundled ES module assets are embedded in the DLL; no external CDN required

## Quick Start

```bash
dotnet add package Dot.QuartzDashboard
```

```csharp
// Program.cs
using QuartzDashboard;

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();

// Line 1: register dashboard services (history tracking included automatically)
builder.Services.AddQuartzDashboard();

var app = builder.Build();

app.UseAuthentication();  // if using auth
app.UseAuthorization();   // if using auth

// Line 2: mount the dashboard (before MapControllers / MapFallbackToFile)
app.UseQuartzDashboard();

app.MapControllers();
app.Run();
```

Open **`/quartz`** in your browser.

## Dashboard Pages

| Page | What you see |
|------|-------------|
| **Overview** | Scheduler info + stat cards with SVG sparkline execution trends + last error card |
| **Jobs** | All jobs with inline trigger details, last run time, live search/filter, server-side pagination, trigger/pause/resume/delete, batch operations |
| **Triggers** | Grouped by job (accordion with persistent expand/collapse state), schedule descriptions, relative fire times, next-fire previews |
| **Executing** | Currently running jobs with animated duration bars |
| **History** | Paginated fire events with relative duration bars, job filter, CSV export, history count badge |
| **Graph** | Dual-line SVG chart: execution count + avg duration + error rate, zoom toggles, duration overlay |
| **Timeline** | Full-width color-coded execution bars with crosshair tooltip, auto-fit range, pulsing now-marker |
| **Health** | Success rate, failed executions, thread pool utilization bar, scheduler diagnostics |
| **Calendars** | Quartz calendars list with type badges and descriptions |
| **Settings** | Refresh interval slider, per-page auto-refresh toggles, history retention info, data management |

Auto-refreshes every 5 seconds. Dark/light theme with OS auto-detection. Responsive mobile layout with bottom tab bar. Collapsible sidebar. Sticky table headers. Sortable columns. Keyboard shortcuts (`?` to see all). Global search (`Ctrl+K`). Branded boot loader. SignalR reconnection toasts. Data pulse indicator. Embed mode (`?embed=true`).

## Configuration

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/admin/scheduler";    // default: "/quartz"
    options.Enabled = true;               // false = UseQuartzDashboard() is a no-op
    options.ReadOnly = false;             // disable all write actions
    options.UseSignalR = true;            // real-time updates (registers hub automatically)

    // Auth
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Admin"];          // role whitelist
    options.RequiredPolicy = "CanViewDashboard"; // named policy (takes priority over roles)

    // History limits
    options.MaxFireHistory = 500;
    options.MaxExecutionLogsPerJob = 50;
    options.HistoryRetentionHours = 24;
    options.PersistHistoryToSqlite = "quartz-history.db";
    options.Title = "My App Dashboard";
});
```

### SQLite persistent history

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.PersistHistoryToSqlite = "quartz-history.db";
});
```

Use SQLite when you want fire history to survive restarts. If omitted, the dashboard keeps history in memory unless you set `PersistHistoryPath` for JSON persistence.

### Dark mode

The UI automatically follows the system light/dark preference in v3.0.0. No extra option is required.

### Bind from appsettings.json

```json
{
  "QuartzDashboard": {
    "Enabled": true,
    "Path": "/quartz",
    "ReadOnly": false,
    "UseSignalR": true,
    "RequireAuthentication": false,
    "RequiredPolicy": "",
    "AllowedRoles": [],
    "MaxFireHistory": 500,
    "MaxExecutionLogsPerJob": 50,
    "HistoryRetentionHours": 24,
    "PersistHistoryToSqlite": "quartz-history.db",
    "Title": "QuartzDash"
  }
}
```

```csharp
builder.Services.AddQuartzDashboard(options =>
    builder.Configuration.GetSection("QuartzDashboard").Bind(options));
```

### Environment gating

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Enabled = !builder.Environment.IsProduction();
});
```

### Authentication & Authorization

Three levels, checked in order:

1. **`RequireAuthentication`** — unauthenticated requests → 401
2. **`RequiredPolicy`** — uses `IAuthorizationService` (named policy) → 403 on failure
3. **`AllowedRoles`** — role whitelist, checked if no policy is set → 403 on failure

```csharp
// Role-based
builder.Services.AddQuartzDashboard(options =>
{
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Admin", "Operator"];
});

// Policy-based
builder.Services.AddAuthorization(o =>
    o.AddPolicy("RequireDashboardAccess", p => p.RequireRole("Admin")));

builder.Services.AddQuartzDashboard(options =>
{
    options.RequireAuthentication = true;
    options.RequiredPolicy = "RequireDashboardAccess";
});
```

## Migrating from v2.x to v3.0.0

1. Remove `builder.Services.AddQuartzDashboardHistory();` — `AddQuartzDashboard()` now registers history automatically.
2. Remove any `UseSystemFonts` option usage — system fonts are now the default in v3.0.0.
3. Enjoy the smaller package — bundled/minified assets cut package size by about 50%, with no code changes required.

## Middleware Placement

```csharp
app.UseAuthentication();   // ← must be BEFORE UseQuartzDashboard if using auth
app.UseAuthorization();    // ← must be BEFORE UseQuartzDashboard if using auth

app.UseQuartzDashboard();  // ← BEFORE MapControllers and MapFallbackToFile

app.MapControllers();
app.MapFallbackToFile("index.html"); // e.g. Blazor WASM
```

> ⚠️ **Blazor WASM users**: placing `UseQuartzDashboard()` after `MapFallbackToFile` will cause all `/quartz` requests to return `index.html` instead of the dashboard.

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
| GET | `/jobs` | All jobs with triggers + schedule descriptions (paginated: `?offset=0&limit=50`) |
| GET | `/jobs/{group}/{name}` | Single job detail with JobDataMap |
| POST | `/jobs/{group}/{name}/trigger` | Fire job immediately |
| POST | `/jobs/{group}/{name}/pause` | Pause job |
| POST | `/jobs/{group}/{name}/resume` | Resume job |
| POST | `/jobs/{group}/{name}/interrupt` | Interrupt executing job |
| DELETE | `/jobs/{group}/{name}` | Delete job |

### Triggers

| Method | Path | Description |
|--------|------|-------------|
| GET | `/triggers` | All triggers with schedule descriptions (paginated: `?offset=0&limit=50`) |
| GET | `/triggers/{group}/{name}` | Single trigger detail |
| POST | `/triggers/{group}/{name}/pause` | Pause trigger |
| POST | `/triggers/{group}/{name}/resume` | Resume trigger |

### Runtime

| Method | Path | Description |
|--------|------|-------------|
| GET | `/executing` | Currently executing jobs with duration |
| GET | `/history` | Paginated fire events (`?offset=0&limit=50&job=key`) |
| GET | `/stats` | Per-minute execution buckets, rate, avg duration, P50/P95/P99 |
| GET | `/stats/history` | Rolling history for the graph |
| GET | `/health` | Success rate, thread pool utilization, failure list |
| GET | `/timeline` | Execution timeline data (up to 500 records) |
| GET | `/heatmap` | Execution density grid (day-of-week × hour-of-day with success rates) |
| GET | `/calendars` | Quartz calendars list |
| GET | `/schedulers` | All registered schedulers (name, instance ID, status) |
| GET | `/config` | Dashboard config snapshot |

## SignalR Real-Time Updates

When `UseSignalR = true` (default), the NuGet registers its own hub automatically:

```
Hub endpoint: {Path}/hub  (e.g. /quartz/hub)
```

> **Do NOT** call `app.MapHub<QuartzDashboardHub>()` yourself — it is handled internally.

To verify the hub is active:
```bash
curl -X POST http://localhost:5000/quartz/hub/negotiate?negotiateVersion=1
# → 200 OK = working
```

## History & Stats

`AddQuartzDashboard()` automatically registers an `IJobListener` that:

- Records the last **N fire events** (configurable via `MaxFireHistory`, default 500)
- Persists history to SQLite with `PersistHistoryToSqlite`, or to JSON with `PersistHistoryPath` if you prefer a file-based fallback
- Auto-prunes records older than `HistoryRetentionHours` (default 24h)
- Buckets executions **per-minute** into 120 rolling `ExecutionBucket` entries
- Tracks per-bucket: count, total duration, error count
- Powers `/api/stats`, `/api/stats/history`, the timeline, and CSV export from the History page

No external storage is required — in-memory works out of the box. For production or longer-lived audit trails, SQLite is the recommended option.

## Testing

```bash
# Run core unit tests (95 tests)
dotnet test QuartzDashboard.Tests -c Release

# Run integration tests (61 tests — real WebApplicationFactory with Quartz scheduler)
dotnet test QuartzDashboard.IntegrationTests -c Release

# Run all tests (156 total)
dotnet test -c Release
```

Integration tests verify: endpoint responses, auth flows, config options, SignalR hub connectivity, read-only mode, host app coexistence, and history tracking — all with a realistic simulated API host.

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `/quartz` returns Blazor `index.html` | `UseQuartzDashboard()` placed after `MapFallbackToFile` | Move it before |
| Dashboard loads but SignalR shows amber/disconnected | Hub not registered | Set `UseSignalR = true` (default), do not manually call `MapHub` |
| 401 on all dashboard requests | `RequireAuthentication = true` but user not logged in | Add auth middleware before `UseQuartzDashboard()` |
| SQLite history does not persist | The app cannot write to the configured database path | Use a writable relative or absolute path for `PersistHistoryToSqlite` |
| History/stats stay empty after upgrade | Legacy setup still relies on old history wiring assumptions | Keep `AddQuartzDashboard()` and remove any old `AddQuartzDashboardHistory()` call |
| Browser shows stale UI after upgrading | Old assets are cached locally | Hard refresh once after upgrading to v3.0.0 |

## Architecture

```
Request → app.Use() (inline middleware, path-matched to basePath)
          ├── /hub/*                → pass through to SignalR endpoint routing
          ├── /api/*                → feature-specific handlers in `Handlers/`
          ├── /quartz               → 302 redirect → /quartz/
          ├── /app.min.js           → embedded esbuild JavaScript bundle
          ├── /app.min.css          → embedded esbuild stylesheet bundle
          ├── /charts.min.js        → embedded chart bundle
          └── anything else         → SPA fallback (embedded index.html)
```

- **Backend**: Raw ASP.NET Core `app.Use()` middleware — zero routing conflicts with controllers
- **Handlers**: API logic is split by feature into the `Handlers/` directory
- **Models**: Request/response contracts live in `Models/`
- **Services**: Runtime helpers such as history persistence and execution buckets live in `Services/`
- **Frontend**: ES modules bundled/minified with esbuild, then embedded into the DLL
- **Assets**: Fully embedded — no external CDN or CSP allowlist needed for dashboard assets
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `Quartz` 3.18.0, `Quartz.Extensions.DependencyInjection` 3.18.0
- **Strong-named**: Assembly is signed for GAC/enterprise scenarios

## Demo

```bash
cd QuartzDashboard.Demo

dotnet run                     # default port 5190
dotnet run -- -p 8080          # custom port
dotnet run -- --auth           # enable cookie auth (test access control)
dotnet run -- --readonly       # disable write actions
dotnet run -- -p 5000 --auth --readonly
```

6 demo jobs with diverse schedules: HealthCheck (15s), CacheWarmup (30s), ReportGeneration (2min), DataSync (CRON :00/:30), UnstableImport (~30% fail rate), ManualNotification (durable, fire from UI).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full version history.

### v3.0.0 (2026-05-10)
- Dark mode with system preference detection
- SQLite persistent history
- Next-N-fires trigger preview
- CSV history export
- Favicon failure badge
- Faster job search/filter
- ~50% package size reduction through bundled/minified embedded assets

## License

MIT — use it, ship it, open-source it.

