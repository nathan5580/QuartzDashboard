# Dot.QuartzDashboard

A beautiful, self-contained **Quartz.NET scheduler dashboard** — drop it into any ASP.NET Core app with two lines of code.

![Dark UI](https://img.shields.io/badge/UI-Dark_Alpine.js_Tailwind-6366f1)
![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4)
![NuGet](https://img.shields.io/badge/NuGet-Dot.QuartzDashboard-004880)
![Version](https://img.shields.io/nuget/v/Dot.QuartzDashboard)

---

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
// Optional: add per-minute execution history and stats graph:
builder.Services.AddQuartzDashboardHistory();

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
    options.MaxFireHistory = 100;            // max fire records in memory
    options.MaxExecutionLogsPerJob = 50;     // max log lines per job
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
    "MaxFireHistory": 100,
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
- The NuGet handles /quartz → /quartz/ redirect automatically (v2.1.10+)

--- API ENDPOINTS (all under {Path}/api/) ---
GET  /scheduler           - scheduler metadata, status, uptime
POST /scheduler/start     - start or resume from standby
POST /scheduler/standby   - put scheduler in standby
GET  /jobs                - all jobs with triggers and schedule descriptions
GET  /jobs/{group}/{name} - single job detail with JobDataMap
POST /jobs/{group}/{name}/trigger  - fire job immediately
POST /jobs/{group}/{name}/pause    - pause job
POST /jobs/{group}/{name}/resume   - resume job
POST /jobs/{group}/{name}/interrupt - interrupt executing job
DELETE /jobs/{group}/{name}        - delete job
GET  /triggers            - all triggers with schedule descriptions
POST /triggers/{group}/{name}/pause   - pause trigger
POST /triggers/{group}/{name}/resume  - resume trigger
GET  /executing           - currently running jobs with duration
GET  /history             - last N fire events (requires AddQuartzDashboardHistory())
GET  /stats               - per-minute execution buckets + rates
GET  /stats/history       - rolling history for the graph
GET  /health              - scheduler health, success rate, failure list
GET  /timeline            - execution timeline data
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
- CSP headers: if you have a Content-Security-Policy, allow cdn.jsdelivr.net in script-src, style-src, and connect-src (Alpine.js + SignalR load from CDN)
```

---

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work
- **Control** the scheduler — start, standby, trigger jobs, pause/resume/delete jobs and triggers
- **Track** execution history with per-minute bucketed stats and live SVG charts
- **Monitor** execution rate, average duration, and error trends in real time
- **Secure** your dashboard with authentication, role-based access, and authorization policies
- **Zero build step** — single HTML SPA with Alpine.js + Tailwind CDN, all embedded in the DLL

## Quick Start

```bash
dotnet add package Dot.QuartzDashboard
```

```csharp
// Program.cs
using QuartzDashboard;

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();

// Line 1: register dashboard services
builder.Services.AddQuartzDashboard();

// Optional: track fire history for stats graph
builder.Services.AddQuartzDashboardHistory();

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
| **Overview** | Scheduler info + stat cards with SVG sparkline execution trends |
| **Jobs** | All jobs with inline trigger details, live search/filter, trigger/pause/resume/delete |
| **Triggers** | Grouped by job (accordion), schedule descriptions, relative fire times |
| **Executing** | Currently running jobs with animated duration bars |
| **History** | Last N fire events with relative duration bars, job filter |
| **Graph** | Dual-line SVG chart: execution count + avg duration, zoom toggles |
| **Timeline** | Color-coded execution dots with tooltips, real-time now-line |
| **Health** | Success rate, failed executions, pool utilization, scheduler diagnostics |
| **Calendars** | Quartz calendars list with type badges and descriptions |
| **Settings** | Refresh interval slider, per-page auto-refresh toggles, data management |

Auto-refreshes every 5 seconds. Dark theme, responsive, collapsible sidebar.

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
    options.MaxFireHistory = 100;
    options.MaxExecutionLogsPerJob = 50;
});
```

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
    "MaxFireHistory": 100,
    "MaxExecutionLogsPerJob": 50
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
| GET | `/jobs` | All jobs with triggers + schedule descriptions |
| GET | `/jobs/{group}/{name}` | Single job detail with JobDataMap |
| POST | `/jobs/{group}/{name}/trigger` | Fire job immediately |
| POST | `/jobs/{group}/{name}/pause` | Pause job |
| POST | `/jobs/{group}/{name}/resume` | Resume job |
| POST | `/jobs/{group}/{name}/interrupt` | Interrupt executing job |
| DELETE | `/jobs/{group}/{name}` | Delete job |

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
| GET | `/history` | Last N fire events (requires `AddQuartzDashboardHistory()`) |
| GET | `/stats` | Per-minute execution buckets, rate, avg duration |
| GET | `/stats/history` | Rolling history for the graph |
| GET | `/health` | Success rate, pool utilization, failure list |
| GET | `/timeline` | Execution timeline data |
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

`AddQuartzDashboardHistory()` registers an `IJobListener` that:

- Records the last **N fire events** in a `ConcurrentQueue<FireRecord>` (configurable via `MaxFireHistory`)
- Buckets executions **per-minute** into 120 rolling `ExecutionBucket` entries
- Tracks per-bucket: count, total duration, error count
- Powers `/api/stats`, `/api/stats/history`, and the SVG execution graph

No external storage — all in-memory, ~7 KB for 120 buckets.

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `/quartz` returns Blazor `index.html` | `UseQuartzDashboard()` placed after `MapFallbackToFile` | Move it before |
| Dashboard loads but SignalR shows amber/disconnected | Hub not registered | Set `UseSignalR = true` (default), do not manually call `MapHub` |
| CDN scripts blocked | Strict `Content-Security-Policy` header | Add `cdn.jsdelivr.net` to `script-src`, `style-src`, `connect-src` |
| 401 on all dashboard requests | `RequireAuthentication = true` but user not logged in | Add auth middleware before `UseQuartzDashboard()` |
| Jobs not showing in dashboard | History not enabled | Call `AddQuartzDashboardHistory()` for stats/history endpoints |

## Architecture

```
Request → app.Use() (inline middleware, path-matched to basePath)
          ├── /hub/*       → pass through to MapHub endpoint routing (SignalR)
          ├── /api/*       → HandleApi (route by path segments)
          ├── /quartz      → 302 redirect → /quartz/ (v2.1.10+)
          ├── /app.js      → embedded static file
          ├── /app.css     → embedded static file
          └── anything else → SPA fallback (embedded index.html)
```

- **Backend**: Raw ASP.NET Core `app.Use()` middleware — zero routing conflicts with controllers
- **Frontend**: Single HTML file with Alpine.js 3.x + Tailwind CSS v4 CDN (all embedded in the DLL)
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

5 demo jobs with diverse schedules: HealthCheck (15s), CacheWarmup (30s), ReportGeneration (2min), DataSync (CRON :00/:30), ManualNotification (durable, fire from UI).

## Changelog

### v2.1.10 (2026-05-04)
- Fixed: `/quartz` (no trailing slash) now automatically redirects to `/quartz/` — relative asset URLs (`app.js`, `app.css`) were resolving to the wrong base path

### v2.1.9 (2026-05-04)
- Consolidated setup: `UseSignalR = true` now registers the SignalR hub internally — no manual `app.MapHub<QuartzDashboardHub>()` required
- All configuration keys aligned with `QuartzDashboardOptions` property names (`Path`, `RequiredPolicy`, `TrackHistory`)

### v2.0.0 (2026-05-03)
- Breaking: `QuartzDashboardOptions` additions: `Enabled`, `RequireAuthentication`, `AllowedRoles`, `RequiredPolicy`, `MaxFireHistory`, `MaxExecutionLogsPerJob`
- `UseQuartzDashboard()` is a no-op when `Enabled = false`
- Authentication support with role-based and policy-based authorization
- Strong-named assembly for GAC/enterprise scenarios
- Package icon and SourceLink support
- New demo CLI flags: `-p`, `--auth`, `--readonly`

### v1.0.0 (2026-05-02)
- Complete UI/UX overhaul: glassmorphism, collapsible sidebar, animations, responsive
- Live execution graph: SVG dual-line chart with zoom toggles and tooltips
- New `/api/stats` endpoint with per-minute execution buckets
- Schedule descriptions on triggers ("Every 00:00:30", CRON expressions)
- Expandable job rows with inline trigger details

### v0.3.0 (2026-05-02)
- Fixed routing via `app.Use()` for Blazor WASM compatibility (replaced `app.Map()`)

### v0.2.0 (2026-05-02)
- Raw middleware approach, all endpoints verified

## License

MIT — use it, ship it, open-source it.


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
