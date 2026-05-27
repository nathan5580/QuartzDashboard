# Dot.QuartzDashboard

<p align="center">
  <img src="https://raw.githubusercontent.com/nathan5580/QuartzDashboard/main/assets/logo.svg" width="200" alt="Dot.QuartzDashboard">
</p>

A self-contained, embedded **Quartz.NET scheduler dashboard** for ASP.NET Core. Two-line install, live SignalR updates, dark mode, persistent history, secure by default.

[![NuGet](https://img.shields.io/nuget/v/Dot.QuartzDashboard?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/Dot.QuartzDashboard)
[![Downloads](https://img.shields.io/nuget/dt/Dot.QuartzDashboard?style=flat-square&logo=nuget&color=green)](https://www.nuget.org/packages/Dot.QuartzDashboard)
[![Build](https://img.shields.io/github/actions/workflow/status/nathan5580/QuartzDashboard/dotnet.yml?branch=main&style=flat-square&logo=github)](https://github.com/nathan5580/QuartzDashboard/actions)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://opensource.org/licenses/MIT)

<p align="center">
  <img src="https://raw.githubusercontent.com/nathan5580/QuartzDashboard/main/ux-audit-screenshots/overview-dark.png" alt="Overview page in dark mode" width="900">
  <br>
  <em>Overview page (dark mode). See <a href="#dashboard-pages">all pages</a> below.</em>
</p>

---

## Contents

- [What's New in v4.2.x](#whats-new-in-v42x)
- [What it does](#what-it-does)
- [Quick Start](#quick-start)
- [Dashboard Pages](#dashboard-pages)
- [Configuration](#configuration)
  - [SQLite persistent history](#sqlite-persistent-history)
  - [Dark mode](#dark-mode)
  - [Bind from appsettings.json](#bind-from-appsettingsjson)
  - [Environment gating](#environment-gating)
  - [Authentication & Authorization](#authentication--authorization)
- [Migrating from v3.x to v4.0](#migrating-from-v3x-to-v40)
- [Middleware Placement](#middleware-placement)
- [API Endpoints](#api-endpoints)
- [SignalR Real-Time Updates](#signalr-real-time-updates)
- [History & Stats](#history--stats)
- [Testing](#testing)
- [Common Issues](#common-issues)
- [Architecture](#architecture)
- [Demo](#demo)
- [🤖 AI Prompt](#-ai-prompt)
- [Changelog](#changelog)
- [License](#license)

---

## What's New in v4.2.x

**v4.2 is the security-defaults release** — two breaking default flips that make a misconfigured deployment fail closed instead of fail open.

- **`RequireAuthentication` now defaults to `true`.** Before v4.2 the dashboard accepted anonymous requests by default, which on an open port meant anonymous remote job control. From v4.2 you must wire up `UseAuthentication()` / `UseAuthorization()` (or explicitly opt back into anonymous with `options.RequireAuthentication = false` plus the startup warning).
- **CSRF guard: `RequireCsrfHeader` defaults to `true`.** Mutating endpoints require `X-Requested-With: XMLHttpRequest` or `X-CSRF-Token`. The bundled SPA sends the header automatically; custom front-ends must add it.
- **Defensive security headers**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin` on dashboard-owned responses only.
- **`prefers-reduced-motion` respected** across every dashboard animation.
- **Toast queue announced to screen readers** via `aria-live="polite"`.
- **SignalR bridge memory leak fixed** across host recycles — handlers now unsubscribe in `StopAsync`.
- **N+1 trigger-state lookup eliminated** on `/api/jobs` and `/api/triggers` — `GetTriggerState` is batched via `Task.WhenAll`, dropping latency on schedulers with hundreds of triggers.
- **File history store** canonicalizes paths via `Path.GetFullPath` so writes always land somewhere debuggable.
- **`/api/import`** now surfaces `placeholderJobs[]` and a `placeholderWarning` when an `IJob` type can't be resolved at import time.
- **Polling fallback timer leak after page unload** fixed — `pagehide` and `beforeunload` stop the interval and SignalR connection.
- **`failedHistory` `:key` collision** fixed (composite key includes `fireInstanceId + fireTime + index`).
- **`FireRecord` properties are now `{ get; init; }`** — immutable across consumers and thread-safe by construction.
- **Per-request `CancellationToken` propagation** — `ApiRouteContext.Ct` is bound to `HttpContext.RequestAborted` and flows into Quartz scheduler calls.

### v4.2.2 — two-round persona audit (2026-05-27)

Twelve focused commits from a 14-persona audit pass. Drop-in upgrade from 4.2.1. Full notes in [CHANGELOG.md](CHANGELOG.md#422--2026-05-27); highlights:

- **Closed a stored XSS** in the timeline row-action overlay (CWE-79) and added defence-in-depth name validation on every create / import endpoint. Security headers now ship on API responses too. CSP-friendly: no more inline `onclick=` in the bundled SPA.
- **ETag short-circuit** for static assets — Day-2 visits 304 instead of redownloading ~264 kB. `index.html` is cached as a `byte[]` after first token-replace.
- **Idle tabs stop polling** — `document.hidden` + `visibilitychange` catch-up.
- **Full WCAG 2.2 AA pass**: keyboard-operable job rows + drawer-as-dialog with focus restore, skip-to-content link, `aria-label` on color-only signals, sidebar contrast lift, color-blind / forced-colors hardening.
- **Mobile responsive cleanup**: Triggers right-edge clip, Graph chip overflow, Timeline `1023.8 m[s]` clip, 44 × 44 pt tap-target floor.
- **`AddQuartzDashboard` is now idempotent**; `QuartzDashboardOptions` is sealed. Dead `QuartzDashboardAuthMiddleware` deleted; source-generated regex for scheduler-name validation.
- **`<html lang>` + `dir`** set from `navigator.language` at boot; locale-aware durations via `Intl.NumberFormat`.
- **Brand**: unified on the NuGet icon's Q-ring mark (favicon + sidebar + boot splash were three different marks before).
- **Tests**: unit suite **116 / 116 green** for the first time — pre-existing stale assertions updated to match the actual handler shapes.

### v4.2.1 fixes (post-audit)

- **SignalR `Subscribe` no longer rejects** when the dashboard's `RequireAuthentication` is `false`. The method-level `[Authorize]` overrode the hub endpoint's policy, leaving auth-off clients with a permanent "Real-time connection lost" banner.
- **Jobs page Alpine `:aria-expanded` page-error** on every render fixed — `(undefined && ...).toString()` is now `(!! (...)).toString()`.
- **Auth 401/403 returns an HTML error page** for browser navigations (`Accept: text/html`) instead of a raw JSON blob. Curl / fetch / XHR clients still get JSON.
- **Demo** — `Program.cs` now explicitly sets `RequireAuthentication = authMode` so plain `dotnet run` (no flags) lands on a working dashboard instead of a 401.

### Migration from v4.1.x

```diff
  builder.Services.AddQuartzDashboard(options =>
  {
      options.Path = "/quartz";
+     // v4.2: defaults flipped to secure. Set explicitly only if you have
+     // an external auth / anti-forgery layer or run on a trusted network.
+     options.RequireAuthentication = false;
+     options.RequireCsrfHeader = false;
  });
```

Otherwise: wire up `app.UseAuthentication()` / `app.UseAuthorization()` and set `options.AllowedRoles` (or `options.RequiredPolicy`) — see [Authentication & Authorization](#authentication--authorization).

For older release notes (v4.1, v4.0, v3.x) see [CHANGELOG.md](CHANGELOG.md).

---

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work in real time
- **Control** the scheduler — start, standby, trigger jobs, pause / resume / delete jobs and triggers
- **Navigate** between pages with a single click — clickable stat cards on the Overview jump directly to their section
- **Track** execution history with date-range filters (1h / 6h / 24h), inline error previews, CSV export, server-side pagination, and live SVG charts
- **Pin** key jobs to the Overview dashboard for a permanent heads-up view
- **Preview** the next scheduled fire times with next-N-fires trigger inspection
- **Monitor** execution rate, average duration, P50/P95/P99 percentiles, and error trends
- **Search** jobs instantly with inline filters; global search across jobs, triggers, and history with `Ctrl+K`
- **Navigate** with keyboard shortcuts — `?` to see all; `G J/T/H/E/G/L/S/O` to jump to any page
- **Inspect** failed runs at a glance — error snippets appear inline in the History table; full stacktrace in one click
- **Build** CRON expressions visually with the built-in builder and presets
- **Alert** on job failures via callbacks, webhooks, or the favicon failure badge
- **Secure** with authentication, role-based access, and authorization policies
- **Persist** fire history to SQLite, JSON, or in-memory storage
- **Embed** the dashboard in iframes with `?embed=true` (strips sidebar/header)
- **Adapt** automatically to dark or light mode; fully responsive with mobile bottom tab bar
- **Stay self-contained** — bundled ES module assets embedded in the DLL; no external CDN required

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
| **Overview** | Clickable stat cards (Jobs, Triggers, Executing Now, Total Executions) with sparklines · scheduler uptime · last error card · pinned jobs · upcoming schedule preview · pin affordance hint |
| **Jobs** | All jobs grouped by group · inline trigger details · relative last-run time · live search/filter with mobile toggle · sortable columns · trigger / pause / resume / delete / view history · server-side pagination |
| **Triggers** | Grouped by job (accordion) · schedule descriptions · relative last-fire / next-fire times · pause / resume / delete per trigger |
| **Executing** | Currently running jobs with animated duration bars · fire instance ID · live elapsed time · interrupt action |
| **History** | Paginated fire events · inline error snippets on failed rows · date-range quick filters (1h / 6h / 24h / All) · status filter · full stacktrace on click · CSV + JSON export |
| **Graph** | Dual-line SVG chart: execution count + avg duration + error rate · zoom toggles · duration overlay |
| **Timeline** | Full-width Gantt bars color-coded per job · crosshair tooltip · auto-fit range · pulsing now-marker |
| **Health** | Success rate with record-count context · failed executions trend · thread pool utilization bar · recent failures with error messages · scheduler diagnostics |
| **Calendars** | Quartz calendars list with type badges and descriptions |
| **Settings** | Refresh interval slider · per-page auto-refresh toggles · history retention info · keyboard shortcuts reference |

Auto-refreshes every 5 seconds via SignalR. Dark/light theme with OS auto-detection. Fully responsive — mobile bottom tab bar covers all 10 sections (scrollable). Collapsible sidebar. Sticky/sortable table headers. Full keyboard navigation (`?` for shortcut reference). Global search (`Ctrl+K`) with deduped history results. Favicon failure badge. Embed mode (`?embed=true`).

#### Iframe Embedding

Append `?embed=true` to the dashboard URL to strip the sidebar and header for a cleaner embedded experience:

```html
<iframe src="https://yourapp.com/quartz?embed=true"
        style="width: 100%; height: 700px; border: none;"
        title="Quartz Dashboard">
</iframe>
```

In embed mode, the sidebar navigation, top header, and breadcrumbs are hidden. All pages, API endpoints, and real-time features remain fully functional.

## Configuration

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/admin/scheduler";    // default: "/quartz"
    options.Enabled = true;               // false = UseQuartzDashboard() is a no-op
    options.ReadOnly = false;             // disable all write actions (see Read-Only Mode below)
    options.UseSignalR = true;            // real-time updates (registers hub automatically)

    // Auth
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Admin"];          // role whitelist
    options.RequiredPolicy = "CanViewDashboard"; // named policy (takes priority over roles)

    // History limits
    options.MaxFireHistory = 500;
    options.MaxExecutionLogsPerJob = 50;
    options.HistoryRetentionHours = 24;
    options.PersistHistoryPath = "quartz-history.json";  // optional JSON persistence
    options.Title = "My App Dashboard";

    // Alerts
    options.OnJobFailed = async (jobKey, ex) => { /* Slack/PagerDuty */ };
    options.WebhookUrl = "https://hooks.slack.com/...";
});
```

### Custom history store (Postgres, Redis, Mongo, …)

Implement the two-method `IFireHistoryStore` interface (from `Dot.QuartzDashboard.Abstractions`)
and register it as a singleton **after** `AddQuartzDashboard()` — it will replace the default
in-memory store. The dashboard reads through the interface; you do not need to fork the package
to add a new backend.

```csharp
using QuartzDashboard.Abstractions;
using Npgsql;

public sealed class PostgresFireHistoryStore : IFireHistoryStore, IDisposable
{
    private readonly NpgsqlDataSource _db;
    public PostgresFireHistoryStore(string connectionString) =>
        _db = NpgsqlDataSource.Create(connectionString);

    public int Count
    {
        get
        {
            using var conn = _db.OpenConnection();
            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM fire_history", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public event Action<FireRecord>? OnFireRecorded;

    public void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime,
        TimeSpan duration, bool success, int refireCount = 0,
        string? exceptionMessage = null, string? exceptionType = null)
    {
        using var conn = _db.OpenConnection();
        using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fire_history
              (job_key, trigger_key, fire_time, duration_ticks, success, refire_count, exception_message, exception_type)
            VALUES (@j, @t, @f, @d, @s, @r, @em, @et)
            """, conn);
        cmd.Parameters.AddWithValue("@j", jobKey);
        cmd.Parameters.AddWithValue("@t", triggerKey);
        cmd.Parameters.AddWithValue("@f", fireTime.UtcDateTime);
        cmd.Parameters.AddWithValue("@d", duration.Ticks);
        cmd.Parameters.AddWithValue("@s", success);
        cmd.Parameters.AddWithValue("@r", refireCount);
        cmd.Parameters.AddWithValue("@em", (object?)exceptionMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@et", (object?)exceptionType ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        OnFireRecorded?.Invoke(new FireRecord
        {
            JobKey = jobKey,
            TriggerKey = triggerKey,
            FireTime = fireTime,
            Duration = duration,
            Success = success,
            RefireCount = refireCount,
            ExceptionMessage = exceptionMessage,
            ExceptionType = exceptionType,
        });
    }

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
    {
        using var conn = _db.OpenConnection();
        using var cmd = new NpgsqlCommand(
            """
            SELECT job_key, trigger_key, fire_time, duration_ticks, success, refire_count,
                   exception_message, exception_type
            FROM fire_history
            ORDER BY fire_time DESC, id DESC
            LIMIT @count OFFSET @offset
            """, conn);
        cmd.Parameters.AddWithValue("@count", count);
        cmd.Parameters.AddWithValue("@offset", offset);

        using var reader = cmd.ExecuteReader();
        var records = new List<FireRecord>();
        while (reader.Read())
        {
            records.Add(new FireRecord
            {
                JobKey = reader.GetString(0),
                TriggerKey = reader.GetString(1),
                FireTime = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero),
                Duration = TimeSpan.FromTicks(reader.GetInt64(3)),
                Success = reader.GetBoolean(4),
                RefireCount = reader.GetInt32(5),
                ExceptionMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                ExceptionType = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }
        return records;
    }

    public void Clear()
    {
        using var conn = _db.OpenConnection();
        using var cmd = new NpgsqlCommand("DELETE FROM fire_history", conn);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}

// Program.cs
builder.Services.AddQuartzDashboard();
builder.Services.AddSingleton<IFireHistoryStore>(
    new PostgresFireHistoryStore(builder.Configuration.GetConnectionString("Quartz")!));
```

Contract notes:

- **Singleton lifetime** — the same instance is shared across the scheduler listener, the
  REST handlers, and the SignalR bridge. Make implementations thread-safe.
- **`RecordFire` is called on the Quartz thread pool** — keep it short and non-blocking, or
  buffer writes internally (the SQLite store coalesces to once per second; consider similar).
- **`GetRecent(count, offset)` is the hot read path.** Return the newest record first. The
  dashboard calls it on every refresh, so make it indexed-by-fire-time descending.
- **`OnFireRecorded` is optional** — fire it after the write succeeds; the dashboard uses it
  for SignalR real-time fan-out.
- **`Count`** is read on the `/api/health` endpoint; expensive `COUNT(*)`s on huge tables
  should be approximated (e.g., a cached value updated by `RecordFire`).

The SQLite store in `Dot.QuartzDashboard.Sqlite` is a useful reference implementation
covering write coalescing, WAL mode, and indexed lookups.

### SQLite persistent history

SQLite persistence ships in a separate package so the main dashboard NuGet doesn't drag `Microsoft.Data.Sqlite` into apps that don't need it.

```bash
dotnet add package Dot.QuartzDashboard.Sqlite
```

```csharp
using QuartzDashboard.Sqlite;

builder.Services.AddQuartzDashboard();
builder.Services.AddQuartzDashboardSqliteHistory("quartz-history.db");
// Order: call AddQuartzDashboardSqliteHistory AFTER AddQuartzDashboard so it
// replaces the default in-memory store registration.
```

Use SQLite when you want fire history to survive restarts. Omit it for in-memory (default), or set `options.PersistHistoryPath` for JSON file persistence.

### Dark mode

The UI automatically follows the system light/dark preference. No option required — the user can also toggle manually from the Settings page.

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
    "PersistHistoryPath": "quartz-history.json",
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

1. **`RequireAuthentication`** (default **`true`** since v4.2) — unauthenticated requests → 401
2. **`RequiredPolicy`** — uses `IAuthorizationService` (named policy) → 403 on failure
3. **`AllowedRoles`** — role whitelist, checked if no policy is set → 403 on failure

The dashboard exposes job-trigger, pause, resume, and delete endpoints. Defaulting to "auth on"
prevents a casual `app.UseQuartzDashboard()` from anonymously exposing remote job control.
Disable explicitly only when the dashboard is reachable solely from a trusted network (the
package logs a startup warning if you do).

### CSRF protection

`RequireCsrfHeader` (default **`true`** since v4.2) blocks mutating endpoints (POST / PUT /
DELETE / PATCH) unless the request carries a custom header — either `X-Requested-With:
XMLHttpRequest` or `X-CSRF-Token: anything`. Browsers cannot send custom headers via simple
cross-origin form submits without triggering a preflight, so the header acts as a
same-origin assertion and stops a logged-in operator's browser from being weaponised by a
malicious page. The bundled SPA always sends the header. Custom front-ends (curl, Postman,
scripts) must add it themselves:

```bash
curl -X POST -H "X-Requested-With: XMLHttpRequest" \
     https://your.app/quartz/api/jobs/demo/MyJob/trigger
```

Disable only if you have an alternative anti-forgery defence (e.g., an upstream gateway that
strips and validates a CSRF cookie); the package logs a startup warning when off.

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

### Read-Only Mode

Set `ReadOnly = true` to expose the dashboard to a wider audience without granting control over the scheduler.

When `ReadOnly = true`:

**Blocked:** Trigger job, Pause/Resume job or trigger, Delete job/trigger/calendar, Start/Standby scheduler, Create/Edit triggers, Interrupt executing jobs.

**Still available:** All GET endpoints, history export (CSV/JSON), print report, real-time updates via SignalR.

Useful for monitoring-only dashboards exposed to a wider audience.

### Multi-Scheduler Support

When multiple Quartz.NET schedulers are registered in the same application, the dashboard automatically detects and displays a scheduler picker in the header. API calls are routed to the selected scheduler via a `?scheduler=SchedulerName` query parameter.

```csharp
// Register multiple schedulers with distinct IDs
builder.Services.AddQuartz(q => { q.SchedulerId = "Primary"; });
builder.Services.AddQuartz(q => { q.SchedulerId = "Secondary"; });

// The dashboard picks up all registered ISchedulerFactory instances automatically
builder.Services.AddQuartzDashboard();
```

No additional configuration is required — the scheduler picker appears automatically when more than one scheduler is detected.

## Migrating from v3.x to v4.0

1. **Update `using` statements for custom history stores.** `IFireHistoryStore` and `FireRecord` moved namespace:
   ```diff
   - using QuartzDashboard.Internal;
   + using QuartzDashboard.Abstractions;
   ```
   Or add `<PackageReference Include="Dot.QuartzDashboard.Abstractions" />` if you only need the interface.

2. **Replace `options.PersistHistoryToSqlite` with the new package + extension method.**
   ```diff
   - builder.Services.AddQuartzDashboard(o =>
   - {
   -     o.PersistHistoryToSqlite = "quartz-history.db";
   - });
   + using QuartzDashboard.Sqlite;
   +
   + builder.Services.AddQuartzDashboard();
   + builder.Services.AddQuartzDashboardSqliteHistory("quartz-history.db");
   ```
   Add `<PackageReference Include="Dot.QuartzDashboard.Sqlite" />`. The main `Dot.QuartzDashboard` package no longer ships `Microsoft.Data.Sqlite`.

3. **Nothing else changes.** The middleware registration, options surface, dashboard URL, API routes, and JSON wire formats are unchanged.

### Migrating from v2.x to v3.0.0

1. Remove `builder.Services.AddQuartzDashboardHistory();` — `AddQuartzDashboard()` now registers history automatically.
2. Remove any `UseSystemFonts` option usage — system fonts are now the default.
3. Enjoy the smaller package — bundled/minified assets cut package size by ~50%, no code changes required.

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
| POST | `/scheduler/standby` | Put scheduler in standby |

### Jobs

| Method | Path | Description |
|--------|------|-------------|
| GET | `/jobs` | All jobs with triggers + schedule descriptions (`?offset=0&limit=50`) |
| POST | `/jobs` | Create a new job |
| GET | `/jobs/{group}/{name}` | Single job detail with JobDataMap |
| PUT | `/jobs/{group}/{name}` | Update job description / data map |
| DELETE | `/jobs/{group}/{name}` | Delete job |
| GET | `/jobs/{group}/{name}/logs` | Recent execution log lines for a job |
| POST | `/jobs/{group}/{name}/trigger` | Fire job immediately |
| POST | `/jobs/{group}/{name}/pause` | Pause job |
| POST | `/jobs/{group}/{name}/resume` | Resume job |
| POST | `/jobs/{group}/{name}/interrupt` | Interrupt executing job |
| POST | `/jobs/group/{group}/pause` | Pause all jobs in a group |
| POST | `/jobs/group/{group}/resume` | Resume all jobs in a group |
| POST | `/jobs/batch/pause` | Pause a set of jobs by key list |
| POST | `/jobs/batch/resume` | Resume a set of jobs by key list |
| POST | `/jobs/batch/trigger` | Fire a set of jobs immediately |
| POST | `/jobs/batch/delete` | Delete a set of jobs |

### Triggers

| Method | Path | Description |
|--------|------|-------------|
| GET | `/triggers` | All triggers with schedule descriptions (`?offset=0&limit=50`) |
| POST | `/triggers` | Create a new trigger (cron or simple) |
| GET | `/triggers/{group}/{name}` | Single trigger detail |
| PUT | `/triggers/{group}/{name}` | Update trigger schedule / expression |
| DELETE | `/triggers/{group}/{name}` | Unschedule (delete) trigger |
| GET | `/triggers/{group}/{name}/next-fires` | Next N fire times (`?count=10`, max 100) |
| POST | `/triggers/{group}/{name}/pause` | Pause trigger |
| POST | `/triggers/{group}/{name}/resume` | Resume trigger |
| POST | `/triggers/group/{group}/pause` | Pause all triggers in a group |
| POST | `/triggers/group/{group}/resume` | Resume all triggers in a group |

### Calendars

| Method | Path | Description |
|--------|------|-------------|
| GET | `/calendars` | All Quartz calendars |
| POST | `/calendars` | Create a calendar |
| DELETE | `/calendars/{name}` | Delete a calendar |

### Runtime & Diagnostics

| Method | Path | Description |
|--------|------|-------------|
| GET | `/executing` | Currently executing jobs with duration |
| GET | `/history` | Paginated fire events (`?offset=0&limit=50&job=group.name`) |
| GET | `/stats` | Per-minute execution buckets, rate, avg duration, P50/P95/P99 |
| GET | `/stats/history` | Rolling history for the graph |
| GET | `/health` | Success rate, thread pool utilization, failure list |
| GET | `/timeline` | Execution timeline data (up to 500 records) |
| GET | `/heatmap` | Execution density grid (day-of-week × hour-of-day with success rates) |
| GET | `/schedulers` | All registered schedulers (name, instance ID, status) |
| GET | `/config` | Dashboard config snapshot (readonly flag, features, etc.) |

### Utilities

| Method | Path | Description |
|--------|------|-------------|
| POST | `/cron/describe` | Validate a CRON expression and return next 5 fire times |
| GET | `/export` | Export all jobs + triggers as JSON |
| POST | `/import` | Import jobs + triggers from export payload |

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
- Persists history to JSON (`PersistHistoryPath`) if configured, or to SQLite via `AddQuartzDashboardSqliteHistory` (from `Dot.QuartzDashboard.Sqlite`)
- Auto-prunes records older than `HistoryRetentionHours` (default 24h)
- Buckets executions **per-minute** into 120 rolling `ExecutionBucket` entries
- Powers `/api/stats`, `/api/stats/history`, the timeline chart, and CSV/JSON export

No external storage required — in-memory works out of the box. For production use, SQLite is recommended.

## Testing

```bash
# Run core unit tests
dotnet test QuartzDashboard.Tests -c Release

# Run integration tests (real WebApplicationFactory with Quartz scheduler)
dotnet test QuartzDashboard.IntegrationTests -c Release

# Run all tests
dotnet test -c Release
```

Integration tests cover endpoint responses, auth flows, config options, SignalR hub connectivity, read-only mode, host-app coexistence, and history tracking.

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `/quartz` returns Blazor `index.html` | `UseQuartzDashboard()` placed after `MapFallbackToFile` | Move it before |
| SignalR shows amber / disconnected | Hub not registered | Set `UseSignalR = true` (default); do **not** call `MapHub` manually |
| 401 on all dashboard requests | `RequireAuthentication = true` but no auth middleware | Add `UseAuthentication()` / `UseAuthorization()` before `UseQuartzDashboard()` |
| SQLite history does not persist | App cannot write to the configured path | Use a writable relative or absolute path in `AddQuartzDashboardSqliteHistory(...)` |
| History/stats stay empty after upgrade | Stale history wiring | Keep `AddQuartzDashboard()` and remove any old `AddQuartzDashboardHistory()` call |
| Uptime shows raw string like "00:01:23.456" | Using an older build | Upgrade to 3.0.5+ — .NET TimeSpan strings are now parsed correctly |
| Stale UI after upgrading | Cached browser assets | Hard-refresh once (Ctrl+Shift+R) after upgrading |

## Architecture

```
Request → app.Use() (inline middleware, path-matched to basePath)
          ├── /hub/*                → pass through to SignalR endpoint routing
          ├── /api/*                → feature-specific handlers in Handlers/
          ├── /quartz               → 302 redirect → /quartz/
          ├── /app.min.js           → embedded esbuild JavaScript bundle
          ├── /app.min.css          → embedded esbuild stylesheet bundle
          ├── /charts.min.js        → embedded chart bundle
          └── anything else         → SPA fallback (embedded index.html)
```

- **Backend**: Raw ASP.NET Core `app.Use()` middleware — zero routing conflicts with controllers
- **Router**: Declarative `(Method, Pattern, Handler)[]` route table in `ApiRouter` — `{}` wildcard segments, O(routes) dispatch
- **Handlers**: API logic split by feature into `Handlers/`
- **Models**: Typed request/response records in `Models/` (`PagedResponse<T>`, `StatusResponse`, `FireRecordDto`, `ErrorResponse`)
- **Services**: History persistence and execution buckets in `Services/`
- **Frontend**: ES modules bundled/minified with esbuild, embedded into the DLL at build time
- **Assets**: Fully self-contained — no external CDN or CSP allowlist required
- **Packages**: `Dot.QuartzDashboard` (main) · `Dot.QuartzDashboard.Abstractions` (interfaces, no ASP.NET dep) · `Dot.QuartzDashboard.Sqlite` (SQLite store)
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `Quartz` ≥ 3.18.0 < 4.0.0, `Quartz.Extensions.DependencyInjection`

## Demo

```bash
cd QuartzDashboard.Demo

dotnet run                         # default port 5190
dotnet run -- -p 8080              # custom port
dotnet run -- --auth               # enable cookie auth (test access control)
dotnet run -- --readonly           # disable write actions
dotnet run -- --sqlite             # SQLite history (writes to demo-history.db)
dotnet run -- -p 5000 --auth --readonly
```

6 demo jobs with diverse schedules: HealthCheck (15s), CacheWarmup (30s), ReportGeneration (2min), DataSync (CRON :00/:30), UnstableImport (~30% fail rate), ManualNotification (durable, fire from UI).

---

## 🤖 AI Prompt

A copy-pasteable brief covering packages, options, every API endpoint, the SignalR hub, and common mistakes — designed to drop into Copilot / Claude / any coding assistant. See [docs/AI-PROMPT.md](docs/AI-PROMPT.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full version history.

## License

MIT — use it, ship it, open-source it.
