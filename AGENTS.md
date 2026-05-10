# QuartzDashboard — Agent Guide

**NuGet:** `Dot.QuartzDashboard` v2.0.0 — https://www.nuget.org/packages/Dot.QuartzDashboard
**GitHub:** https://github.com/nathan5580/QuartzDashboard
**NuGet:** https://www.nuget.org/packages/Dot.QuartzDashboard

## Overview

A self-contained Quartz.NET scheduler dashboard — drop into any ASP.NET Core app with 2 lines of code. Single HTML SPA (Alpine.js + Tailwind CDN) served as an embedded resource. Multi-targets: net8.0 | net9.0 | net10.0.

## Project Structure

```
QuartzDashboard/
├── QuartzDashboard/                    # Library project (the NuGet package)
│   ├── QuartzDashboard.csproj          # v2.0.0, multi-target, NuGet metadata, SourceLink, icon
│   ├── QuartzDashboardServiceCollectionExtensions.cs  # AddQuartzDashboard() + listeners
│   ├── QuartzDashboardApplicationBuilderExtensions.cs # UseQuartzDashboard() + all API handlers (~1045 lines)
│   ├── QuartzDashboardOptions.cs       # Path, Enabled, ReadOnly, UseSignalR, auth options
│   ├── Internal/
│   │   ├── DashboardEventBus.cs        # In-memory event bus (singleton, decouples from SignalR)
│   │   ├── DashboardSchedulerListener.cs  # ISchedulerListener — lifecycle events → event bus
│   │   ├── IFireHistoryStore.cs        # Fire history abstraction + InMemoryFireHistoryStore
│   │   └── ExecutionLogBuffer.cs       # Per-job in-memory execution log ring buffer
│   ├── SignalR/
│   │   └── QuartzDashboardHub.cs       # SignalR hub + DashboardSignalRBridge (IHostedService)
│   └── wwwroot/
│       └── index.html                  # SPA (~3,453 lines, Alpine.js + Tailwind, embedded resource)
├── QuartzDashboard.Demo/               # Demo app with CLI flags (--auth, --readonly, -p)
│   ├── Program.cs                      # 5 demo jobs, CLI args
│   └── QuartzDashboard.Demo.csproj
├── QuartzDashboard.slnx
├── README.md                           # Full docs — also used as NuGet PackageReadmeFile
├── AGENTS.md                           # This file
└── .github/workflows/dotnet.yml        # CI build pipeline
```

## Key Registration Methods

### `AddQuartzDashboard(options?)`
- Adds singleton `QuartzDashboardOptions` and `DashboardEventBus`
- If `UseSignalR` (default true): adds SignalR + `DashboardSignalRBridge` (IHostedService)

### `UseQuartzDashboard()`
- Uses `app.Map(basePath, branch => ...)` — branches BEFORE endpoint routing to avoid Blazor WASM fallback conflicts
- `app.Map()` creates a sub-pipeline that intercepts `/quartz/*` requests
- API: `/api/scheduler|jobs|triggers|executing|history|stats|timeline` — as patterns handled by HandleApi()
- Static files: serves embedded SPA from `QuartzDashboard.wwwroot` assembly resource
- SignalR hub: `((IEndpointRouteBuilder)app).MapHub<QuartzDashboardHub>("{basePath}/hub")` — maps outside the Map() branch
- 666-line file with ~25 API handler methods + fire history (ConcurrentQueue, 100 records limit)

## NuGet Publishing

Current version: 2.0.0. To publish a new version:

```bash
# Build and pack
cd /Users/home/RiderProjects/QuartzDashboard && dotnet pack QuartzDashboard/QuartzDashboard.csproj -c Release
# Push to nuget.org
dotnet nuget push QuartzDashboard/bin/Release/Dot.QuartzDashboard.2.0.0.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

The NuGet API key is stored in memory. Or use the CI workflow: push a tag `v*` to GitHub.

### Running the Demo
```bash
cd QuartzDashboard.Demo
dotnet run                  # default port 5190
dotnet run -- --auth        # enable auth mode
dotnet run -- --readonly    # read-only mode
dotnet run -- -p 8080       # custom port
```

### csproj NuGet Properties
- `PackageId`: N8.QuartzDashboard
- `Version`: 1.0.0 (bump for releases)
- `PackageLicenseExpression`: MIT
- `PackageReadmeFile`: README.md (from repo root, `../README.md` relative)
- `IncludeSymbols` + `SymbolPackageFormat`: snupkg

## Dependencies
- Quartz 3.18.0
- Quartz.Extensions.DependencyInjection 3.18.0
- Microsoft.AspNetCore.App (FrameworkReference)
- System.Collections.Concurrent (in-box)

## NuGet Package Properties
- `PackageId`: Dot.QuartzDashboard
- `Version`: 2.0.0
- `PackageLicenseExpression`: MIT
- `PackageReadmeFile`: README.md (from repo root)
- `PackageIcon`: icon.png
- `PublishRepositoryUrl`: true
- `IncludeSymbols` + `SymbolPackageFormat`: snupkg
- `EmbeddedFiles`: *.cs for SourceLink



## New in v2.0.0

### Dev-Only Gating (Plan A)
Set `options.Enabled = false` (or use `IHostEnvironment.IsDevelopment()`) to skip dashboard registration entirely in production.

### Auth Integration (Plan B)
`options.RequireAuthentication = true` gates all dashboard routes behind authentication.
`options.AllowedRoles = ["Admin"]` restricts access to specific roles.
`options.RequiredPolicy = "MyPolicy"` uses an ASP.NET Core authorization policy.

### Pagination (Plan C3)
Jobs, triggers, and history endpoints accept `?offset=N&limit=N`. SPA shows "Load More" buttons.

### Batch Operations (Plan C5)
Checkbox-select multiple jobs and pause/resume/trigger/delete in one request.

### Job Execution Logs (Plan C4)
In-memory ring buffer (last 50 entries per job). Viewable in job detail modal.

### Calendar Management (Plan C6)
Full Quartz calendar CRUD: create, list, and delete calendars (holiday, monthly, weekly, daily, cron, annual).

### Misfire Instructions (Plan C7)
Configure misfire handling per trigger: Fire Once Now, Do Nothing, or Ignore Misfire Policy.

### Cron Expression Presets
Ready-to-use presets in the trigger creation UI: every 5 min, hourly, daily midnight, weekdays 9am, etc.

### `/api/config` Endpoint
Exposes readOnly, hasFullAccess, isAuthenticated, basePath for SPA to adapt UI.

## Known Issues & Pitfalls

### SignalR + IApplicationBuilder.MapHub
`UseQuartzDashboard()` uses `((IEndpointRouteBuilder)app).MapHub<>()` which requires the `Microsoft.AspNetCore.Routing` namespace. Works with `<ImplicitUsings>enable</ImplicitUsings>` on .NET 8+.

### Blazor WASM Compatibility
The `app.Map()` pattern is critical — it runs BEFORE endpoint routing, so `MapFallbackToFile("index.html")` in Blazor WASM apps doesn't catch `/quartz/*` routes.

### Fire History In-Memory Only
Fire history (configurable via `MaxFireHistory`) and execution buckets (120 entries) are stored in-memory — lost on app restart. Refactored into `IFireHistoryStore` interface with `InMemoryFireHistoryStore` default. Implement a persistent `SqlServerFireHistoryStore` for production audit trails.

### `IListenerManager` API Compatibility
Quartz 3.10+ uses `IListenerManager` with `AddJobListener`, `AddSchedulerListener`, `AddTriggerListener`. The `GetSchedulerListener` method does NOT exist in this interface. If you see CS1061 about `GetSchedulerListener`, it's a stale build cache — clean and rebuild.
