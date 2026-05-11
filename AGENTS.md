<!--
  This file is for AI coding assistants (Claude, Copilot, Cursor, Codex, etc.).
  Humans: README.md is the friendlier entry point.
-->

# QuartzDashboard Agent Guide

**NuGet:** `Dot.QuartzDashboard` v3.0.3 - https://www.nuget.org/packages/Dot.QuartzDashboard
**GitHub:** https://github.com/nathan5580/QuartzDashboard

## Overview

A self-contained Quartz.NET scheduler dashboard that can be added to an ASP.NET Core app with:

```csharp
builder.Services.AddQuartzDashboard();
app.UseQuartzDashboard();
```

The package multi-targets `net8.0`, `net9.0`, and `net10.0`. The UI is an embedded Alpine.js SPA with bundled/minified assets, no CDN requirement, and SignalR updates enabled by default.

## Project Structure

```text
QuartzDashboard/
├── QuartzDashboard/                    # NuGet library
│   ├── QuartzDashboard.csproj          # package metadata, multi-targeting, SourceLink, embedded assets
│   ├── QuartzDashboardServiceCollectionExtensions.cs
│   ├── QuartzDashboardApplicationBuilderExtensions.cs
│   ├── QuartzDashboardOptions.cs
│   ├── Handlers/                       # API handlers by feature
│   ├── Models/                         # request DTOs
│   ├── Services/                       # runtime services such as execution buckets
│   ├── Internal/                       # event bus, history stores, listeners, helpers
│   ├── Middleware/                     # dashboard auth middleware
│   ├── SignalR/                        # hub + hosted bridge
│   └── wwwroot/                        # embedded SPA assets
├── QuartzDashboard.Tests/              # unit tests
├── QuartzDashboard.IntegrationTests/   # WebApplicationFactory integration tests
├── QuartzDashboard.Demo/               # interactive demo app
├── QuartzDashboard.Sample/             # minimal sample app
├── README.md                           # user docs and NuGet readme
├── CHANGELOG.md
└── .github/workflows/dotnet.yml        # CI, pack, publish
```

## Key Registration Methods

### `AddQuartzDashboard(options?)`

- Registers `QuartzDashboardOptions`, history storage, execution logs, execution buckets, and the event bus.
- Registers SignalR and `DashboardSignalRBridge` when `UseSignalR` is true.
- Attaches job/scheduler listeners so history and live updates work without a separate history call.
- SQLite history is selected when `PersistHistoryToSqlite` is set; JSON file history is selected when `PersistHistoryPath` is set; otherwise history is in memory.

### `UseQuartzDashboard()`

- Uses inline path-matched middleware so `/quartz/*` is handled before host fallback routes.
- Lets `/hub/*` pass through to endpoint routing, then maps `QuartzDashboardHub` at `{Path}/hub` when possible.
- Routes `/api/*` to feature handlers in `Handlers/`.
- Redirects `/quartz` to `/quartz/` so relative embedded assets resolve.
- Serves embedded `index.html`, `app.min.js`, `app.min.css`, `charts.min.js`, favicon, and SignalR client assets.

## Current Package Properties

- `PackageId`: `Dot.QuartzDashboard`
- `Version`: `3.0.4`
- `PackageLicenseExpression`: `MIT`
- `PackageReadmeFile`: `README.md`
- `PackageIcon`: `icon.png`
- `PublishRepositoryUrl`: `true`
- `IncludeSymbols` + `SymbolPackageFormat`: `snupkg`
- Strong-named assembly via `QuartzDashboard.snk`

## Dependencies

- `Quartz` 3.18.0
- `Quartz.Extensions.DependencyInjection` 3.18.0
- `Microsoft.Data.Sqlite` 9.0.5
- `Microsoft.AspNetCore.App` framework reference
- Frontend build: Node.js 20+, `esbuild`

## Build, Test, Pack

```bash
# From the repository root
dotnet build QuartzDashboard.slnx -c Release
dotnet test QuartzDashboard.Tests/QuartzDashboard.Tests.csproj -c Release
dotnet test QuartzDashboard.IntegrationTests/QuartzDashboard.IntegrationTests.csproj -c Release
dotnet pack QuartzDashboard/QuartzDashboard.csproj -c Release

cd QuartzDashboard
npm ci
npm run build
npm run audit
```

## Running the Demo

```bash
cd QuartzDashboard/QuartzDashboard.Demo
dotnet run                  # default port 5190
dotnet run -- --auth        # enable auth mode
dotnet run -- --readonly    # read-only mode
dotnet run -- --sqlite      # SQLite persistent history
dotnet run -- -p 8080       # custom port
```

## Release Flow

CI builds, tests, packs, checks package size, publishes to NuGet, and creates a GitHub release. Push a `v*` tag for a tag-driven release, or push a branch with a new csproj version and let the auto-tag job create `v{Version}`.

Manual pack:

```bash
dotnet pack QuartzDashboard/QuartzDashboard.csproj -c Release
```

Manual push:

```bash
dotnet nuget push QuartzDashboard/bin/Release/Dot.QuartzDashboard.3.0.3.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

## v3 Highlights

- Dark/light mode with system preference detection.
- SQLite-backed persistent history with `PersistHistoryToSqlite`.
- JSON history fallback with `PersistHistoryPath`.
- Next fire previews, CSV history export, health dashboard, graph modes, timeline, keyboard shortcuts, and global search.
- Bundled/minified embedded assets; no CDN allowlist needed.
- `/api/config` exposes UI-safe flags, not raw credential-bearing webhook URLs.

## Known Pitfalls

- `UseQuartzDashboard()` must be registered before `MapFallbackToFile()` in Blazor WASM or SPA hosts.
- If auth is enabled, `UseAuthentication()` and `UseAuthorization()` must run before `UseQuartzDashboard()`.
- Do not manually map `QuartzDashboardHub`; the package maps it when `UseSignalR` is true.
- Fire history is in-memory unless `PersistHistoryToSqlite` or `PersistHistoryPath` is configured.
- Quartz's `IListenerManager` has `AddJobListener`, `AddSchedulerListener`, and `AddTriggerListener`; it does not have `GetSchedulerListener`.
