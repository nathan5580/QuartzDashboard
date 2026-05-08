# Changelog

All notable changes to **Dot.QuartzDashboard** are documented here.

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
