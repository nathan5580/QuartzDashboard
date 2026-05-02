# QuartzDashboard

A beautiful, self-contained Quartz.NET scheduler dashboard built as a NuGet package.

## Features

- **Dark-themed SPA** built with Alpine.js + Tailwind CSS — no build step, no framework dependency
- **View** all jobs, triggers, currently executing jobs, and fire history
- **Control** the scheduler: start, standby, trigger jobs, pause/resume jobs and triggers
- **Zero-config integration** — two lines of code in your ASP.NET Core app
- **Embedded static files** — all assets ship inside the NuGet package

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

Navigate to `/quartz` in your browser.

### Enable Fire History (optional)

```csharp
builder.Services.AddQuartzDashboardHistory();
```

This registers a job listener that records the last 100 job executions for the dashboard's History tab.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/quartz/api/scheduler` | Scheduler metadata and status |
| POST | `/quartz/api/scheduler/start` | Start / resume scheduler |
| POST | `/quartz/api/scheduler/standby` | Pause scheduler |
| GET | `/quartz/api/jobs` | All jobs with triggers |
| GET | `/quartz/api/jobs/{group}/{name}` | Job detail |
| POST | `/quartz/api/jobs/{group}/{name}/trigger` | Fire job now |
| POST | `/quartz/api/jobs/{group}/{name}/pause` | Pause job |
| POST | `/quartz/api/jobs/{group}/{name}/resume` | Resume job |
| GET | `/quartz/api/triggers` | All triggers |
| GET | `/quartz/api/triggers/{group}/{name}` | Trigger detail |
| POST | `/quartz/api/triggers/{group}/{name}/pause` | Pause trigger |
| POST | `/quartz/api/triggers/{group}/{name}/resume` | Resume trigger |
| GET | `/quartz/api/executing` | Currently executing jobs |
| GET | `/quartz/api/history` | Recent fire history |

## Options

```csharp
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/admin/scheduler"; // default: "/quartz"
});
```

## Architecture

- **Backend**: .NET 8+ class library targeting `net8.0;net9.0;net10.0`
- **Frontend**: Single HTML page with Alpine.js for reactivity + Tailwind CSS via CDN
- **Runtime**: Zero external NuGet deps (only Quartz + ASP.NET Core)
- **No registration ceremony** — uses `ISchedulerFactory` from your DI container
