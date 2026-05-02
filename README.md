# QuartzDashboard

A beautiful, self-contained **Quartz.NET scheduler dashboard** — drop it into any ASP.NET Core app with two lines of code.

![Dark UI Preview](https://img.shields.io/badge/UI-Dark_Alpine.js_Tailwind-6366f1)
![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-512BD4)
![NuGet](https://img.shields.io/badge/NuGet-N8.QuartzDashboard-004880)

## What it does

- **See** all your Quartz jobs, triggers, fire schedules, and currently executing work
- **Control** the scheduler — start, standby, trigger jobs, pause/resume jobs and triggers
- **Track** fire history — last 100 executions, durations, and outcomes
- **Zero dependencies** — only Quartz and ASP.NET Core (no JavaScript framework, no build step)

## Quick Start

```bash
dotnet add package N8.QuartzDashboard
```

```csharp
// Program.cs
using QuartzDashboard;

// After AddQuartz() and AddQuartzHostedService():
builder.Services.AddQuartzDashboard();

// After UseRouting():
app.UseQuartzDashboard();
```

Open **`/quartz`** in your browser.

### Enable Fire History (optional)

Records the last 100 job executions so you can see what ran, when, and how long it took.

```csharp
builder.Services.AddQuartzDashboardHistory();
```

## Dashboard UI

| Page | What you see |
|------|-------------|
| **Overview** | Scheduler name, version, uptime, job store, thread pool, quick stats |
| **Jobs** | All scheduled jobs, their triggers (with state), trigger-now/pause/resume buttons |
| **Triggers** | All registered triggers, fire times, pause/resume controls |
| **Executing** | Currently running jobs with elapsed duration |
| **History** | Last 50 fire events — job, trigger, timestamp, duration |

Auto-refreshes every 5 seconds. Dark theme, responsive layout.

## API Endpoints

All endpoints are under `{basePath}/api/` (default: `/quartz/api/`).

### Scheduler

| Method | Path | Description |
|--------|------|-------------|
| GET | `/scheduler` | Metadata, status, uptime, version |
| POST | `/scheduler/start` | Start / resume from standby |
| POST | `/scheduler/standby` | Pause scheduler |

### Jobs

| Method | Path | Description |
|--------|------|-------------|
| GET | `/jobs` | All jobs with nested trigger details |
| GET | `/jobs/{group}/{name}` | Single job detail with JobDataMap |
| POST | `/jobs/{group}/{name}/trigger` | Fire job immediately |
| POST | `/jobs/{group}/{name}/pause` | Pause job |
| POST | `/jobs/{group}/{name}/resume` | Resume job |

### Triggers

| Method | Path | Description |
|--------|------|-------------|
| GET | `/triggers` | All triggers with state and fire times |
| GET | `/triggers/{group}/{name}` | Single trigger detail |
| POST | `/triggers/{group}/{name}/pause` | Pause trigger |
| POST | `/triggers/{group}/{name}/resume` | Resume trigger |

### Runtime

| Method | Path | Description |
|--------|------|-------------|
| GET | `/executing` | Currently executing jobs with duration |
| GET | `/history` | Last 50 fire events (requires `AddQuartzDashboardHistory()`) |

## Configuration

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/admin/scheduler"; // default: "/quartz"
});
```

## Architecture

```
Request → app.UseQuartzDashboard()
          ├── Path matches /quartz/api/* → Handle API (routed by method + path segments)
          ├── Path matches /quartz/*     → Serve static SPA files (from embedded resources)
          └── No match                   → Pass through to next middleware
```

- **Backend**: Raw ASP.NET Core middleware — no `UseEndpoints`/`MapGet` required, zero routing conflicts
- **Frontend**: Single HTML file with Alpine.js 3.x + Tailwind CSS v4 via CDN (29KB, no build step)
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `Quartz` 3.18.0, `Quartz.Extensions.DependencyInjection` 3.18.0

## Demo

A standalone demo app is included in the repo:

```bash
cd QuartzDashboard.Demo
dotnet run
# Open http://localhost:5190/quartz
```

The demo registers three jobs (HealthCheckJob every 30s, CleanupJob every 60s, ManualJob durable-only) so you can see the dashboard in action immediately.

## License

MIT — use it, ship it, open-source it.
