# Changelog

All notable changes to **Dot.QuartzDashboard** are documented here.

## [2.1.34] — 2026-05-08

### Added
- **Job execution log viewer** — "Logs" tab in job drawer shows `ExecutionLogBuffer` entries per job (timestamped, mono-font)
- **Per-job history via direct API** — job drawer History tab now calls `/api/history?job=X` directly (no longer limited to locally loaded rows)
- **Reschedule / edit trigger UI** — pencil icon in trigger row opens Edit modal; supports cron expression and simple interval editing; `PUT /api/triggers/{group}/{name}` backend
- **Job data map editor** — collapsible key-value pair editor when manually triggering a job; passes `dataMap` in POST body
- **Refire count tracking** — `RefireCount` added to `FireRecord`; orange "R" badge shown in history table when > 0
- **Calendar management UI** — new Calendars page in sidebar; lists all calendars, create (holiday/monthly/weekly/daily/cron/annual), delete
- **Misfire policy display** — human-readable misfire instruction label shown in trigger rows and detail; edit via trigger modal dropdown
- **JSON export** — "Export JSON" button next to "Export CSV" on history page; downloads `quartz-history.json`
- **Light mode chart colors** — graph and timeline charts detect `lightMode` and switch to indigo/gray palette appropriate for light backgrounds
- **History pagination** — replaces "Load More" with numbered page buttons (Previous / 1 2 3 … / Next); shows Page X of Y
- **`options.WebhookUrl`** — fire-and-forget HTTP POST on job failure; zero-code alerting for Slack/webhooks/PagerDuty; body includes jobKey, error, fireTime, durationMs
- **Health page failure chart** — 24-bar SVG chart showing failures per hour for the last 24h; bars colored by severity (none=gray, 1-2=amber, 3+=red)
- **95 tests** (up from 93) — new coverage for trigger update fields, dataMap injection, webhookUrl config exposure

### Changed
- `HistoryHandlers.GetFireHistory` now accepts `?job=group.name` query param to filter by job key
- Config API response now includes `historyRetentionHours`, `hasPersistentHistory`, `webhookUrl` (redacted to boolean)

## [2.1.33] — 2026-05-09

### Added
- **`options.Title`** — custom title shown in the sidebar header and browser tab (injected server-side)
- **`options.OnJobFailed`** — async callback fired on every job failure (use for Slack/webhook alerts)
- **`options.HistoryRetentionHours`** — TTL pruning for fire history (default 24h). Records older than the threshold are pruned automatically on write and read
- **`options.PersistHistoryPath`** — persist fire history to a JSON file; history survives restarts. Uses `System.Text.Json` — zero new dependencies
- **Embedded SVG favicon** — clock/gear icon served at `{basePath}/favicon.svg`, linked in `<head>`
- **OS theme detection** — inline script before Alpine loads reads `prefers-color-scheme` on first visit; no flash-of-wrong-theme
- **Keyboard shortcuts overlay** — press `?` to show all shortcuts. Modal with nav keys, action keys, navigation numbers
- **`T` key** — toggle light/dark theme
- **`[` key** — toggle sidebar
- **Multi-scheduler support** — `GET /api/schedulers` lists all registered schedulers; dropdown in sidebar when >1 scheduler detected; `?scheduler=Name` query param routes API calls to the selected scheduler
- **Enhanced cmd+K** — search history records by job key; include trigger names; limit to 15 results when query is empty

### Changed
- Sidebar brand name now uses `config.title` from server instead of hardcoded "QuartzDash"
- `filteredCommands` now includes history and trigger search, capped at 15 for default (no-query) state
- `_api()` now appends `?scheduler=Name` automatically when a non-default scheduler is selected

### Fixed
- Escape key now also closes the shortcuts modal

## [2.1.31] — 2026-05-08

### Added
- `UseSystemFonts` option — skip embedded fonts, use system font stack (saves ~286KB)
- CI auto-publish on `v*` tags with GitHub Release creation
- Failing demo job (`UnstableImport`) to populate Health page error data
- CHANGELOG.md for version tracking

### Changed
- CI workflow now runs tests before publishing
- Publish triggered by version tags (`v2.1.31`) instead of every push to main

## [2.1.30] — 2026-05-08

### Added
- Pre-compiled Tailwind CSS (25KB) — no CDN dependency
- Self-hosted Inter + JetBrains Mono variable fonts (woff2)
- `x-cloak` on body to prevent Alpine.js FOUC
- 7 new tests verifying zero external dependencies

### Removed
- `cdn.tailwindcss.com` runtime dependency
- `fonts.googleapis.com` runtime dependency

## [2.1.29] — 2026-05-08

### Added
- Embedded `signalr.min.js` (v8.0.7) and `alpine.min.js` (v3.14.8)
- `MapQuartzDashboard()` extension for explicit hub registration in test hosts
- SignalR integration tests (8 tests)
- Embedded asset tests (11 tests)

### Fixed
- Safe `IEndpointRouteBuilder` cast in `UseQuartzDashboard()` (no crash in test hosts)

### Removed
- `cdn.jsdelivr.net` dependencies for SignalR and Alpine.js

## [2.1.28] — 2026-05-07

### Features
- Full SPA dashboard: Overview, Health, Jobs, Triggers, History, Timeline, Graph, Executing, Calendars, Settings
- Real-time SignalR updates (job executed, triggered, scheduler status)
- Job CRUD: create, trigger, pause, resume, delete, batch operations
- Trigger CRUD: create (cron/simple), pause, resume, delete
- Calendar CRUD: holiday, monthly, weekly, daily, cron, annual
- Execution graph with count/duration/error sparklines and charts
- Timeline Gantt chart with configurable time ranges
- Command palette (⌘K) for quick navigation
- Dark/Light mode with localStorage persistence
- Read-only mode, authentication, role-based access
- Multi-target: net8.0, net9.0, net10.0
