# Changelog

All notable changes to **Dot.QuartzDashboard** are documented here.

## [2.1.44] — 2026-05-09

### Fixed
- **Green circle artifact on Health page** — Removed `filter="url(#chartGlow)"` from all sparkline SVGs. The `feGaussianBlur` filter created glow artifacts that bled through `overflow:hidden` on stat cards, appearing as large green arcs across the page background.
- **Reduced chartGlow filter region** — Shrunk filter bounds from ±20%/140% to ±10%/120% to prevent any future bleed.

### Improved
- **Light mode contrast overhaul** — Complete rewrite of all light mode CSS overrides for better readability:
  - Headings now use `#0f172a` (near-black) instead of `#111827`.
  - Body text uses `#1e293b` slate-900 for primary, `#334155` for secondary — proper WCAG AA contrast on white.
  - Labels (`text-gray-500`) now map to `#64748b` (slate-500) instead of the too-light `#9ca3af`.
  - Cards use solid `#ffffff` background with `box-shadow` instead of semi-transparent rgba.
  - Badge colors have higher opacity (0.10) and explicit text colors for readability.
  - Toast notifications use deeper background tints and darker text colors.
  - Borders use 0.08 opacity (up from 0.06) for clearer card boundaries.
  - Modals/command palette get solid white backgrounds with subtle drop shadows.
  - SVG chart labels and axis lines adapted for light backgrounds.
  - Spinner border-top now uses indigo accent color in light mode.
  - Drawer backgrounds (`bg-gray-900/950`) override to white/slate-50.

## [2.1.43] — 2026-05-09

### Added
- **Health page skeleton** — shimmer loading state with stat card placeholders, chart skeleton, and diagnostics grid.
- **Inline action spinners** — trigger, pause, resume, and delete buttons now show a CSS spinner while the action is in progress.
- **Card hover effects** — stat cards lift with subtle shadow on hover; card-gradient elements get a soft glow.
- **Table row hover** — consistent highlight on jobs and history table rows (`.table-row-hover`).
- **Stat value transitions** — `.stat-value` class with color transition and tabular-nums for smooth number changes.
- **Toast spring animation** — enter/exit animations use `cubic-bezier(0.16,1,0.3,1)` with scale for a polished feel.

### Improved
- **History skeleton** — upgraded from simple bars to realistic table-row layout with status dots and badge placeholders.
- **Overview stat card skeletons** — enhanced with badge placeholder below the number shimmer.
- **Page transitions** — leave duration reduced from 100ms to 50ms for snappier page switches.
- **Timeline empty state** — now shows a Gantt-chart icon with descriptive title and subtitle.
- **Calendars empty state** — now shows a calendar icon with consistent empty state pattern.
- **Drawer backdrop** — increased to 60% opacity with smoother slide-in/out transitions.
- **Scrollbar styling** — thin 6px scrollbars with subtle hover effect applied globally.

## [2.1.42] — 2026-05-09

### Added
- **URL deep linking** — pages are bookmarkable/shareable via URL hash (`#jobs`, `#history`, `#graph`, etc.). Hash updates on navigation and restores on load.
- **P50/P95/P99 latency percentiles** — overview shows global and per-job percentile cards (P50 green, P95 amber, P99 red) with sample count. Backend computes from fire history.
- **Pinned/favorite jobs** — pin jobs from the Jobs table to see them on the Overview page. Persisted in localStorage. Click to open drawer, shows status + success rate.
- **Mini execution sparkline** — job drawer History tab shows a duration trend polyline above the history list.
- **Schedule preview** — overview shows next 24h upcoming fires with countdown labels.
- **Fullscreen mode** — toggle via button in top toolbar or `F` keyboard shortcut. Shows expand/collapse icon.
- **Duplicate job** — "Duplicate" button in job drawer pre-fills the Create Job modal with the job's config.
- **Sound alerts** — optional audio tone on job failure (toggle in Settings, test button). Uses Web Audio API, no external files.
- **Print summary report** — Settings → About → "Print Report" opens a printable summary with stats, percentiles, job list, and recent failures.
- **Accessibility** — ARIA `role` and `aria-label` on sidebar/main, `aria-current` on active nav item, `:focus-visible` outlines, `.sr-only` utility class.

## [2.1.41] — 2026-05-09

### Added
- **History page search/filter** — filter history by job name and success/error status with a live search bar and status dropdown.
- **Trigger countdown** — trigger rows now show a live countdown to next fire time ("3m", "1h 20m").
- **Execution cost estimator** — job detail drawer shows estimated execution frequency per trigger (runs/hr, runs/day).
- **Overview recent failures** — the overview page shows the 5 most recent failures with exception messages, plus a success rate progress bar with uptime percentage.
- **Overview trend arrows** — stat cards show ↑↓ indicators when values change between refreshes.
- **Footer last refresh** — footer bar displays the last refresh timestamp.

### Fixed
- **Graph offset on first load** — replaced `x-init` (fires once while element is hidden) with `x-effect` + double `requestAnimationFrame` to ensure the container is fully visible and laid out before measuring width. Both the execution graph and timeline chart now render correctly on first navigation.
- **Health chart green circles** — reduced success bar height from 16px to 4px minimum, `rx` from 3→2, opacity from 0.5→0.35. No more oversized green blobs covering the chart.
- **Graph ResizeObserver** — chart containers now use `ResizeObserver` for precise container-aware resizing instead of only `window.resize`.

## [2.1.40] — 2026-05-09

### Added
- **Exception capture** — job execution exceptions are now captured with message, type, and stack trace in fire history, execution logs, and SignalR events.
- **Exception details in History** — click any failed execution row to expand and see the full exception type and message inline.
- **Failure toast notifications** — real-time toast alerts appear when a job fails via SignalR, showing the job name and truncated exception message.
- **Per-job success rate badge** — the Jobs table shows a percentage badge next to each job's status, color-coded green (≥95%), amber (≥80%), or red (<80%).
- **Cron expression validator** — when creating a cron trigger, the expression is validated in real-time with a preview of the next 5 fire times via `POST /api/cron/describe`.
- **Export / Import jobs** — export all jobs and triggers as JSON from the Jobs page header; import from a JSON file to restore jobs. Uses `GET /api/export` and `POST /api/import` endpoints.
- **Comprehensive light mode** — sidebar, main area, drawers, toasts, badges, buttons, modals, scrollbars, command palette, footers, SVG fills, and all `bg-white/[0.0x]` Tailwind classes now have proper light-mode overrides.
- **Mobile responsive improvements** — sidebar collapses to icons at 768px and hides at 480px; tables become card layouts; modals use 95vw width.

### Fixed
- **Health chart visibility** — green success bars now use 16px minimum height and 0.5 opacity (was 3px / 0.25). SVG text visibility uses inline `style` instead of broken `x-show` in SVG context.
- **FileFireHistoryStore** — propagates exception fields to persistent fire history records.
- **SignalR batch payloads** — both drain paths now include `exceptionMessage` in `jobExecutedBatch` events.

## [2.1.39] — 2026-05-09

### Added
- **Trigger search/filter** — search bar on the Triggers page filters in real-time across trigger name, group, job name, job group, state, type, and schedule description. Includes a clear (×) button.
- **Job Data Map inline edit** — the Job Data section in the job detail drawer is now editable. Click **Edit** to enter edit mode with key/value input rows, **+ Add entry** to insert new entries, **×** to remove, and **Save** / **Cancel** to commit or discard changes. Calls `PUT /jobs/{group}/{name}` with the updated data map.
- **Group Pause / Resume** — Jobs page group header rows now show **Pause** / **Resume** buttons (hidden in read-only mode) that pause or resume all jobs in the group at once via `POST /jobs/group/{group}/pause` and `/resume`. Triggers page job rows show equivalent buttons that pause/resume the parent job (and all its triggers).
- **Stale data banner** — a top-of-page banner appears when the scheduler is stopped (`!isStarted`), in standby mode (`isStandbyMode`), or when the real-time SignalR connection is lost (`!signalRConnected`). Shows last-updated time and uses colour-coded severity (red/amber/blue).
- **Distinct sparklines on overview cards** — the four Overview stat cards now show different metrics:
  - *Jobs*: total execution count (unchanged)
  - *Triggers*: success rate trend
  - *Executing Now*: average duration trend
  - *Total Executions*: total execution count (unchanged)

### Fixed
- Trigger group Pause/Resume correctly targets the parent job (pausing the job pauses all its triggers) rather than the trigger group name.



### Fixed
- **Stable job order across refreshes** — jobs, triggers, executing jobs, and timeline lanes now sort deterministically by `group.name` on every refresh. Previously Quartz returned jobs/triggers in HashSet iteration order (non-deterministic), causing the table rows to shuffle on each auto-refresh cycle.
  - `GetAllJobs`: sorted by `(Group, Name)` ascending before pagination
  - `GetAllTriggers`: sorted by `(JobGroup, JobName, Group, Name)` ascending before pagination
  - `GetExecutingJobs`: sorted by `(JobGroup, JobName)` ascending
  - Frontend: defensive `.sort()` added at every assignment point for jobs, triggers, and executingJobs
  - Timeline lane labels: `timelineVisibleLabels` and `timelineYLabels` sorted alphabetically so row positions are stable across live updates

## [2.1.37] — 2026-05-08

### Fixed
- **Sidebar icon sizing** — nav item SVG icons now use inline `style="width:20px;height:20px"` instead of Tailwind `class="w-5 h-5"`. Since Tailwind only scans `index.html` at build time, class names inside `app.js` strings were not generated, causing icons to expand beyond their container and potentially clip adjacent text labels.
- **Health failure chart layout** — bars now fill the full SVG width dynamically. Previously bar positions were hardcoded (`x = 44 + idx * 27`), causing 5 buckets to occupy only the leftmost ~15% of the 720px chart. Bar X and width are now computed as a fraction of the available 668px inner width, evenly distributing all buckets.
- **Health failure chart states** — bars use green fill-opacity when `errorRate = 0` (success) instead of invisible minimum-height red slivers. A "✓ No failures detected" overlay appears when all buckets have zero error rate. Grid lines at 50% and 100% added for better readability.
- **Health page auto-refresh** — `health` page was missing from `autoRefreshPages` defaults, so it never refreshed on the auto-refresh timer. Now included with `true` by default alongside all other pages.

## [2.1.36] — 2026-05-08

### Fixed
- **Graph x-axis mixed timestamp formats** — `executionBuckets` from `/api/stats` now returns `minute` as a full ISO 8601 timestamp (`ToString("o")`) instead of `"HH:mm"`. Previously live-mode padding used `toISOString()` while real buckets used `"09:52"` format, making labels render inconsistently (some as "11:48 AM", others as "09:52" raw). Now all points parse via `new Date(isoString)` and format uniformly.
- **Health page failure chart labels** — added `Label` (`"HH:mm"`) field to execution bucket response. Health chart template was already using `bucket.label` but the field was missing from the API response, causing all axis labels to be empty. Both `minute` (ISO for JS date math) and `label` (short display string) are now returned.
- **Graph padding `label` field** — zero-pad buckets now include a `label` field alongside `minute` so they are consistent with real buckets.



### Fixed
- **Bar chart x-axis alignment** — bars in the Graph page bar-chart mode are now centered directly under their corresponding x-axis time labels. Previously `barRects` used `i/n` positioning while labels used `i/(n-1)`, causing a visible horizontal offset. All modes (bar, line, area, heatmap) now share consistent coordinate mapping.
- **Job drawer tab flickering** — all four drawer tabs (Overview, Triggers, History, Logs) converted from `<template x-if>` to `<div x-show>`. This eliminates DOM destroy/recreate on every tab click, preventing flash-of-content and Alpine.js cloneNode errors during rapid tab switching.
- **History tab internal states** — loading skeleton and empty state inside History tab now use `x-show` instead of `x-if` for consistent no-flicker transitions.

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
