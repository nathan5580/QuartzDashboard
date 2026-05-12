<!--
  This file is for AI coding assistants (Claude, Copilot, Cursor, Codex, etc.).
  Humans: README.md is the friendlier entry point.
-->

# QuartzDashboard Agent Guide

**NuGet packages (v4.1.0):**
- `Dot.QuartzDashboard` — https://www.nuget.org/packages/Dot.QuartzDashboard
- `Dot.QuartzDashboard.Abstractions` — https://www.nuget.org/packages/Dot.QuartzDashboard.Abstractions
- `Dot.QuartzDashboard.Sqlite` — https://www.nuget.org/packages/Dot.QuartzDashboard.Sqlite

**GitHub:** https://github.com/nathan5580/QuartzDashboard

## Overview

A self-contained Quartz.NET scheduler dashboard that can be added to an ASP.NET Core app with:

```csharp
builder.Services.AddQuartzDashboard();
app.UseQuartzDashboard();
```

The packages multi-target `net8.0`, `net9.0`, and `net10.0`. The UI is an embedded Alpine.js SPA with bundled/minified assets, no CDN requirement, and SignalR updates enabled by default.

## Project Structure

```text
QuartzDashboard/
├── QuartzDashboard/                     # main NuGet library (Dot.QuartzDashboard)
│   ├── QuartzDashboard.csproj           # package metadata, multi-targeting, SourceLink, embedded assets
│   ├── QuartzDashboardServiceCollectionExtensions.cs
│   ├── QuartzDashboardApplicationBuilderExtensions.cs
│   ├── QuartzDashboardOptions.cs        # mutable options
│   ├── IQuartzDashboardOptions.cs       # read-only contract (handlers depend on this)
│   ├── Handlers/                        # API handlers by feature
│   ├── Models/                          # request DTOs + response records (PagedResponse, StatusResponse, ErrorResponse, FireRecordDto)
│   ├── Services/                        # runtime services such as execution buckets
│   ├── Internal/                        # event bus, in-memory + file history stores, listeners, helpers
│   ├── Middleware/                      # dashboard auth middleware
│   ├── SignalR/                         # hub + hosted bridge
│   ├── icon.svg / icon.png              # NuGet package icon
│   ├── package.json / build-assets.mjs  # esbuild bundling for SPA assets
│   └── wwwroot/                         # embedded SPA assets (Alpine + Tailwind)
├── QuartzDashboard.Abstractions/        # Dot.QuartzDashboard.Abstractions package
│   └── (IFireHistoryStore, FireRecord)  # implement this to plug a custom store
├── QuartzDashboard.Sqlite/              # Dot.QuartzDashboard.Sqlite package
│   └── SqliteFireHistoryStore.cs        # opt-in persistent store (WAL, indexed by job key)
├── QuartzDashboard.Tests/               # unit tests
├── QuartzDashboard.IntegrationTests/    # WebApplicationFactory integration tests
├── QuartzDashboard.Demo/                # interactive demo app
├── QuartzDashboard.Sample/              # minimal sample app
├── README.md                            # user docs and NuGet readme
├── CHANGELOG.md
└── .github/workflows/dotnet.yml         # CI, pack, auto-tag, publish to NuGet
```

## Key Registration Methods

### `AddQuartzDashboard(options?)`

- Registers `QuartzDashboardOptions` and `IQuartzDashboardOptions` (read-only contract), history storage (in-memory default), execution buckets, execution logs, and the event bus.
- Registers SignalR and `DashboardSignalRBridge` when `UseSignalR` is true.
- Attaches job/scheduler listeners so history and live updates work without a separate history call.
- `PersistHistoryPath` (JSON file history) still lives in the main package.
- For SQLite history, install `Dot.QuartzDashboard.Sqlite` and call `AddQuartzDashboardSqliteHistory("history.db")` AFTER `AddQuartzDashboard()`.

### `UseQuartzDashboard()`

- Uses inline path-matched middleware so `/quartz/*` is handled before host fallback routes.
- Lets `/hub/*` pass through to endpoint routing, then maps `QuartzDashboardHub` at `{Path}/hub` when possible.
- Routes `/api/*` to feature handlers via `ApiRouter` (declarative route table — see `Routing/ApiRouter.cs`).
- Redirects `/quartz` to `/quartz/` so relative embedded assets resolve.
- Serves embedded `index.html` (with `__QUARTZ_VERSION__` substitution), `app.min.js`, `app.min.css`, `charts.min.js`, favicon, and SignalR client assets.

## Current Package Properties

- `PackageId`s: `Dot.QuartzDashboard`, `Dot.QuartzDashboard.Abstractions`, `Dot.QuartzDashboard.Sqlite`
- `Version`: `4.1.0`
- `PackageLicenseExpression`: `MIT`
- `PackageReadmeFile`: `README.md` (main package; Abstractions/Sqlite have their own)
- `PackageIcon`: `icon.png` (all three packages)
- `PublishRepositoryUrl`: `true`
- `IncludeSymbols` + `SymbolPackageFormat`: `snupkg`
- Strong-named assemblies via `QuartzDashboard.snk`

## Dependencies

- `Quartz` `[3.18.0, 4.0.0)`
- `Quartz.Extensions.DependencyInjection` `[3.18.0, 4.0.0)`
- `Microsoft.AspNetCore.App` framework reference
- `Microsoft.Data.Sqlite` 9.0.5 — only in the `Dot.QuartzDashboard.Sqlite` package, not the main one
- Frontend build: Node.js 20+, `esbuild`

## Build, Test, Pack

```bash
# From the repository root
dotnet build QuartzDashboard.slnx -c Release
dotnet test QuartzDashboard.Tests/QuartzDashboard.Tests.csproj -c Release
dotnet test QuartzDashboard.IntegrationTests/QuartzDashboard.IntegrationTests.csproj -c Release
dotnet pack QuartzDashboard/QuartzDashboard.csproj -c Release
dotnet pack QuartzDashboard.Abstractions/QuartzDashboard.Abstractions.csproj -c Release
dotnet pack QuartzDashboard.Sqlite/QuartzDashboard.Sqlite.csproj -c Release

# Frontend assets (esbuild bundle of Alpine + Tailwind)
cd QuartzDashboard
npm ci
npm run build
npm run audit
```

The `dotnet build` step invokes `npm run build` automatically via the csproj target, so frontend assets are always fresh in CI.

## Running the Demo / Sample

```bash
cd QuartzDashboard.Demo
dotnet run                  # default port 5190
dotnet run -- --auth        # enable auth mode
dotnet run -- --readonly    # read-only mode
dotnet run -- --sqlite      # SQLite persistent history (references Dot.QuartzDashboard.Sqlite)
dotnet run -- -p 8080       # custom port

# Minimal sample
dotnet run --project QuartzDashboard.Sample/QuartzDashboard.Sample.csproj  # port 5200
```

## Release Flow

CI builds, tests, packs all three packages, checks package size (2MB limit), publishes to NuGet, and creates a GitHub release. Push a `v*` tag for a tag-driven release, or push to `main` with a new csproj version and let the `auto-tag` job create `v{Version}` automatically. The `publish` job then runs on the tag and pushes all three nupkgs.

Manual pack + push:

```bash
dotnet pack QuartzDashboard/QuartzDashboard.csproj -c Release
dotnet nuget push QuartzDashboard/bin/Release/Dot.QuartzDashboard.4.1.0.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

## v4 Highlights

### v4.1.0 — UX polish + anti-flicker
- **In-place refresh** via `mergeArrayInPlace(existing, incoming, keyFn)` — Alpine `x-for` reuses DOM nodes, so auto-refreshes don't destroy state (scroll position, open drawers, expanded rows).
- **Silent background refresh** — `loadX(silent=true)` skips loading spinners and error toasts; SignalR fan-out and auto-refresh use this path.
- **Row density toggle** (comfortable/compact), persisted to localStorage.
- **Desktop notifications** for job failures (opt-in browser permission).
- **Per-job sparkline column** on Jobs page (visible from `xl` ≥1280px).
- **"In-memory only" banner** on History when no persistent store is registered.
- **Triggers group header** shows context-aware Pause/Resume + paused counter.
- Bug fixes: health nav badge position, sparkline column never visible (Tailwind pre-build gap), timeline tooltip epoch flash, graph CURRENT RATE unit mislabel, history trigger truncation, executing empty state icon.

### v4.0.0 — package split
- Split into three NuGet packages: `Dot.QuartzDashboard`, `Dot.QuartzDashboard.Abstractions`, `Dot.QuartzDashboard.Sqlite`. The main package no longer depends on `Microsoft.Data.Sqlite`.
- `IQuartzDashboardOptions` read-only contract.
- Response DTOs in `QuartzDashboard.Models`: `PagedResponse<T>`, `StatusResponse`, `ErrorResponse`, `FireRecordDto` (wire format unchanged).
- `ApiRouter` declarative route table replaces the 250-line if/else chain.

### v3 legacy
- Dark/light mode with system preference detection.
- JSON history fallback with `PersistHistoryPath`.
- Next fire previews, CSV history export, health dashboard, graph modes, timeline, keyboard shortcuts, and global search.
- Bundled/minified embedded assets; no CDN allowlist needed.
- `/api/config` exposes UI-safe flags, not raw credential-bearing webhook URLs.

## Frontend Architecture Notes

- `wwwroot/src/main.js` is the esbuild entry. It imports per-feature sections (`createApiSection`, `createSignalRSection`, `createJobsSection`, etc.) and merges them via `mergeSections(...)` into one Alpine `dashboard` data object.
- State is centralized in `src/state.js` (`createState()`).
- Each load function (`loadJobs`, `loadTriggers`, `loadHistory`, `loadExecutingJobs`, `refreshAll`) accepts a `silent` flag. Silent calls skip `loading.*` toggles and error toasts. Always pass `silent=true` from auto-refresh / SignalR; pass `silent=false` (default) for user-initiated actions.
- Anti-flicker invariant: never reassign array references (`this.jobs = newArray`). Always use `this.mergeArrayInPlace(this.jobs, sorted, keyFn)`. Job-drawer data holds a reference to the same object inside `this.jobs`, so in-place mutation updates the drawer automatically.
- Settings persistence: `qd-settings` localStorage key holds `sidebarOpen`, `graphChartMode`, `refreshInterval`, `historyLimit`, `collapsedGroups`, `rowDensity`. `saveSettings()` writes the bundle; init restores it.
- Tailwind: `wwwroot/tailwind.css` is a pre-built static file from a prior Tailwind v3 generation. It does NOT include `xl:` or `2xl:` breakpoint utilities. If you need those, add explicit media queries in `wwwroot/styles/responsive.css` (see the `xl:table-cell` / `2xl:table-cell` definitions there).

## Known Pitfalls

- `UseQuartzDashboard()` must be registered before `MapFallbackToFile()` in Blazor WASM or SPA hosts.
- If auth is enabled, `UseAuthentication()` and `UseAuthorization()` must run before `UseQuartzDashboard()`.
- Do not manually map `QuartzDashboardHub`; the package maps it when `UseSignalR` is true.
- Fire history is in-memory unless `PersistHistoryPath` is configured OR `Dot.QuartzDashboard.Sqlite` is referenced and `AddQuartzDashboardSqliteHistory(...)` is called after `AddQuartzDashboard()`.
- Quartz's `IListenerManager` has `AddJobListener`, `AddSchedulerListener`, and `AddTriggerListener`; it does not have `GetSchedulerListener`.
- Restart the host app to pick up changes to `wwwroot/` files — they are served as embedded resources, not from disk.
- When changing Alpine `x-for` keyed lists, prefer in-place mutation over reassignment to avoid full DOM teardown.
