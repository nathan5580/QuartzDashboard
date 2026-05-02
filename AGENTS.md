# QuartzDashboard — Agent Guide

**NuGet:** `Dot.QuartzDashboard` v1.0.0 — https://www.nuget.org/packages/Dot.QuartzDashboard
**GitHub:** https://github.com/nathan5580/QuartzDashboard

## Overview

A self-contained Quartz.NET scheduler dashboard — drop into any ASP.NET Core app with 2 lines of code. Single HTML SPA (Alpine.js + Tailwind CDN) served as an embedded resource. Multi-targets: net8.0 | net9.0 | net10.0.

## Project Structure

```
QuartzDashboard/
├── QuartzDashboard/                    # Library project (the NuGet package)
│   ├── QuartzDashboard.csproj          # Multi-target, NuGet metadata (MIT, readme, symbols)
│   ├── QuartzDashboardServiceCollectionExtensions.cs  # AddQuartzDashboard() + AddQuartzDashboardHistory()
│   ├── QuartzDashboardApplicationBuilderExtensions.cs # UseQuartzDashboard() + all API handlers (666 lines)
│   ├── QuartzDashboardOptions.cs       # Path, ReadOnly, UseSignalR options
│   ├── Internal/
│   │   ├── DashboardEventBus.cs        # In-memory event bus (singleton, decouples listeners from SignalR)
│   │   ├── DashboardSchedulerListener.cs  # ISchedulerListener — lifecycle events → event bus
│   │   └── QuartzDashboardOptions.cs   # (same as root, symlinked or copied)
│   ├── SignalR/
│   │   └── QuartzDashboardHub.cs       # SignalR hub + DashboardSignalRBridge (IHostedService)
│   └── wwwroot/
│       └── index.html                  # SPA (~65KB, Alpine.js + Tailwind, embedded resource)
├── QuartzDashboard.Demo/               # Demo app with 5 sample jobs
│   ├── Program.cs
│   └── QuartzDashboard.Demo.csproj
├── QuartzDashboard.slnx
├── README.md                           # Full docs — also used as NuGet PackageReadmeFile
└── AGENTS.md                           # This file
```

## Key Registration Methods

### `AddQuartzDashboard(options?)`
- Adds singleton `QuartzDashboardOptions` and `DashboardEventBus`
- If `UseSignalR` (default true): adds SignalR + `DashboardSignalRBridge` (IHostedService)

### `AddQuartzDashboardHistory()`
- Adds `DashboardListenerAttacher` (IHostedService) + `DashboardSchedulerListener` (ISchedulerListener)
- `DashboardListenerAttacher.StartAsync`: gets IScheduler, attaches IJobListener + ISchedulerListener to it
- `DashboardJobListener.JobWasExecuted`: records fire history + publishes to event bus

### `UseQuartzDashboard()`
- Uses `app.Map(basePath, branch => ...)` — branches BEFORE endpoint routing to avoid Blazor WASM fallback conflicts
- `app.Map()` creates a sub-pipeline that intercepts `/quartz/*` requests
- API: `/api/scheduler|jobs|triggers|executing|history|stats|timeline` — as patterns handled by HandleApi()
- Static files: serves embedded SPA from `QuartzDashboard.wwwroot` assembly resource
- SignalR hub: `((IEndpointRouteBuilder)app).MapHub<QuartzDashboardHub>("{basePath}/hub")` — maps outside the Map() branch
- 666-line file with ~25 API handler methods + fire history (ConcurrentQueue, 100 records limit)

## NuGet Publishing

Current version: 1.0.0. To publish a new version:

```bash
# Build and pack
cd QuartzDashboard && dotnet pack -c Release
# Push to nuget.org
dotnet nuget push bin/Release/N8.QuartzDashboard.1.0.0.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

The NuGet API key is stored in memory (see `memory` tool — target 'memory', search 'nuget').

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

## Known Issues & Pitfalls

### SignalR + IApplicationBuilder.MapHub
`UseQuartzDashboard()` uses `((IEndpointRouteBuilder)app).MapHub<>()` which requires the `Microsoft.AspNetCore.Routing` namespace. Works with `<ImplicitUsings>enable</ImplicitUsings>` on .NET 8+.

### Blazor WASM Compatibility
The `app.Map()` pattern is critical — it runs BEFORE endpoint routing, so `MapFallbackToFile("index.html")` in Blazor WASM apps doesn't catch `/quartz/*` routes.

### Fire History In-Memory Only
Fire history (100 records) and execution buckets (120 entries) are stored in static `ConcurrentQueue` and `ConcurrentDictionary` — lost on app restart. Adequate for development/demo, not for production persistency.

### `IListenerManager` API Compatibility
Quartz 3.10+ uses `IListenerManager` with `AddJobListener`, `AddSchedulerListener`, `AddTriggerListener`. The `GetSchedulerListener` method does NOT exist in this interface. If you see CS1061 about `GetSchedulerListener`, it's a stale build cache — clean and rebuild.
