# Changelog

All notable changes to this project are documented here.

## [3.0.3] — 2026-05-10

### Added
- Dark mode with automatic system preference detection
- SQLite-backed persistent history via `PersistHistoryToSqlite`
- Next-N-fires trigger preview for upcoming schedules
- CSV export from the History page
- Favicon failure badge for at-a-glance job health
- Faster job search/filter in the Jobs page
- Bundled/minified embedded assets (~50% smaller package)
- Browser notifications for job failures
- Global search (Cmd+K) across jobs, triggers, and history
- Keyboard shortcuts cheat sheet (press `?`)
- Health Dashboard page with failure stats, misfire count, thread pool gauge
- Job Detail Modal with 5 tabs (Metadata, JobDataMap editor, Execution History, Triggers, JSON Export/Clone)
- Graph overhaul with 3 modes: Line, Bar chart, Heatmap
- Timeline zoom/pan with "Now" pulse line
- Batch UX with Shift+click range selection and progress indicators
- Light mode toggle

### Changed
- Backend handlers extracted from monolith into `Handlers/` directory (per-feature)
- Models extracted into `Models/` directory with `[JsonPropertyName]` attributes
- SignalR event batching: `Channel<T>` producer/consumer, 100ms windows
- Thread-safe stats: `ConcurrentDictionary` + `Interlocked` replaces broken `ConcurrentQueue` pattern
- N+1 query fix: executing jobs fetched once into `HashSet<JobKey>`
- Batch deduplication: 4 batch methods → 1 delegate pattern
- Slimmer middleware: ~288 lines → ~90 lines

### Fixed
- Quartz 3.x static `LogProvider` `ObjectDisposedException` in tests (reflection cleanup + CollectionFixture)
- Caching headers: `index.html` gets `no-cache`, other assets get `public, max-age=86400`
- All `fetch()` calls now use `this.api(endpoint)` — no hardcoded `/quartz` paths
- Google Fonts `&display=swap` + system font fallback chain

### Breaking
- `AddQuartzDashboardHistory()` is no longer required — history is auto-registered
- `UseSystemFonts` option removed — system fonts are now the default

## [2.4.1] — 2026-04-20

### Fixed
- Test fixture `ObjectDisposedException`: added `[CollectionDefinition("QuartzDashboard")]` + reflection-based `LogProvider` cleanup
- All 95 unit tests passing (was 12/95)

## [2.4.0] — 2026-04-15

### Added
- Quartz configuration UI (create/edit jobs, triggers, calendars from the dashboard)
- Cron expression tester with next-5-fire-times preview
- Batch job operations (pause, resume, delete multiple)
- Shift+click range selection
- SignalR real-time updates via `QuartzDashboardHub`
- Integration test suite (61 tests, `WebApplicationFactory`)

### Changed
- Migrated to .NET 10.0 multi-targeting (net8.0; net9.0; net10.0)
- Assembly strong-named for GAC/enterprise scenarios
- Frontend rebuilt as Alpine.js SPA (~2,900 lines)

## [2.0.0] — 2025-08-01

### Added
- Complete SPA dashboard with multiple pages (overview, jobs, triggers, history, executing)
- API endpoints for job/trigger/scheduler management
- In-memory fire history with per-minute stats
- Execution graph and timeline views
- Auth middleware with role-based and policy-based access control
- Embed mode (`?embed=true`)
- NuGet packaging with embedded assets

### Changed
- Renamed from `Dot.QuartzPanel` to `Dot.QuartzDashboard`

## [1.0.0] — 2025-03-01

### Added
- Initial release as `Dot.QuartzPanel`
- Basic Quartz.NET monitoring dashboard
- Job list and trigger views
