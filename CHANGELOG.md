# Changelog

All notable changes to **Dot.QuartzDashboard** are documented here.

## [4.2.0] — 2026-05-16

Security hardening, perf, and accessibility polish. Two **breaking** default changes that
make a misconfigured deployment fail closed instead of fail open — see the migration note
at the bottom of the section.

### ⚠️ Breaking changes

- **`QuartzDashboardOptions.RequireAuthentication` default flipped from `false` → `true`.**
  The dashboard exposes job-trigger, pause, resume, and delete endpoints; allowing anonymous
  access by default is effectively anonymous remote job control. Adopters running on a trusted
  network must explicitly set `options.RequireAuthentication = false`. The package logs a
  startup warning when that flag is off.
- **New `QuartzDashboardOptions.RequireCsrfHeader` option, default `true`.** Mutating
  endpoints (POST / PUT / DELETE / PATCH) require an `X-Requested-With: XMLHttpRequest` or
  `X-CSRF-Token` header. Blocks the classic CSRF attack where a logged-in operator visiting
  a hostile page triggers job mutations from their session. The bundled SPA always sends the
  header; custom front-ends and scripts must add it themselves. Disable only if you have an
  upstream anti-forgery defence (a startup warning is logged when off).

### Added
- **CSRF guard middleware** on the dashboard API surface (see above).
- **Defensive security headers** on dashboard responses: `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`. Applied
  only to responses the dashboard owns; host-app responses are untouched.
- **`prefers-reduced-motion` respect** — all dashboard animations (`pulse-dot`, `boot-float`,
  `due-blink`, `status-ring-pulse`, etc.) collapse to ≤0.01ms when the OS/browser signals
  reduced-motion preference.
- **Toast queue announced to screen readers** via `role="status" aria-live="polite"
  aria-atomic="true"` — success/error messages are now picked up by assistive tech.
- **README guide for custom `IFireHistoryStore`** — worked Postgres example covering the
  thread-safety, ordering, and `OnFireRecorded` contract.
- **`PackageReleaseNotes`** populated and **descriptions differentiated** across all three
  NuGet packages so NuGet.org cards stop looking identical.
- **`EmbedUntrackedSources` + `DeterministicSourcePaths`** added to all three csproj files
  so SourceLink resolves correctly inside debuggers.

### Fixed
- **SignalR bridge memory leak across host recycles** — `DashboardSignalRBridge.StopAsync`
  now `-=` unsubscribes every handler it attached to the singleton `DashboardEventBus`. A
  subsequent `StartAsync` no longer stacks a second set of handlers on top of the first.
- **N+1 trigger-state lookup** on `/api/jobs` and `/api/triggers` — `GetTriggerState` was
  called per trigger inside the response-building loop, dominating latency on schedulers
  with >50 triggers. Now batched in parallel via a single `Task.WhenAll`.
- **`FileFireHistoryStore` path canonicalization** — relative paths with `..` segments are
  now resolved via `Path.GetFullPath` so writes land somewhere debuggable instead of an
  unexpected relative location.
- **`/api/import` silent placeholder fallback** — jobs whose original `IJob` type can't be
  resolved at import time used to be silently replaced with `PlaceholderJob`. The response
  now includes `placeholderJobs[]` and a `placeholderWarning` so the operator knows.
- **Polling fallback timer leak after page unload** — `pagehide` and `beforeunload` listeners
  stop the interval (and the SignalR connection) so the dashboard goes quiet when navigated
  away from.
- **`failedHistory` `:key` collision** — composite key (`fireInstanceId + fireTime + index`)
  prevents Alpine `x-for` from reusing the wrong DOM node when `fireInstanceId` is missing.

### Changed
- **`FireRecord` properties are now `{ get; init; }`** — records returned from
  `IFireHistoryStore` are immutable across consumers and thread-safe by construction.
- **`DashboardEventBus`, `DashboardEvent`, and the event records moved from `public` to
  `internal`.** They live under `QuartzDashboard.Internal` and were not intended as
  extension points; consumers couldn't reach them through a sanctioned API anyway, so this
  closes a future-break risk. Custom integrations that need to publish dashboard events
  should depend on the public `IFireHistoryStore` abstraction instead.
- **`CancellationToken` propagation** — `ApiRouteContext.Ct` is bound to
  `HttpContext.RequestAborted`. `GetAllJobs`, `GetAllTriggers`, `GetCurrentlyExecutingJobs`,
  the inline group-pause/resume route handlers, and `cron/describe` now thread the token
  into every Quartz scheduler call. Full per-handler coverage is targeted for v4.3.

### Migration from v4.1.x

Existing apps that previously relied on the open defaults:

```diff
  builder.Services.AddQuartzDashboard(options =>
  {
      options.Path = "/quartz";
+     // v4.2: defaults flipped to secure. Restore prior behaviour only if you have
+     // an external auth / anti-forgery layer.
+     options.RequireAuthentication = false;
+     options.RequireCsrfHeader = false;
  });
```

Otherwise: wire up `app.UseAuthentication()` / `app.UseAuthorization()` and set
`options.AllowedRoles` (or `options.RequiredPolicy`) — see the README's
*Authentication & Authorization* section.

## [4.1.0] — 2026-05-12

UI polish, anti-flicker refresh, and new UX features. No API changes; drop-in upgrade from 4.0.x.

### Added
- **In-place refresh (no flicker)** — `mergeArrayInPlace` mutates job/trigger/history arrays in place, so Alpine `x-for` reuses DOM nodes instead of destroying and recreating them. Scroll position, open drawers, and expanded rows survive auto-refreshes.
- **Silent background refresh** — all `loadX(...)` functions accept a `silent` flag; auto-refresh and SignalR fan-out call them silently, skipping loading spinners and error toasts. Visible refresh actions remain loud.
- **Scroll preservation** — `refreshPage(page, silent)` saves and restores `scrollTop` across the silent refresh so the page doesn't jump.
- **Row density toggle** — `comfortable` / `compact` mode in Settings, persisted to localStorage and applied via `data-density` on `<body>`.
- **Desktop notifications** — opt-in browser notifications for job failures. Permission state and enable flag persisted to localStorage.
- **Per-job sparkline column** on the Jobs page (Trend), visible from `xl` (1280px). Shows duration trend across recent executions.
- **History "in-memory only" banner** — dismissible amber banner on the History page when no persistent store is registered. Reads `config.hasPersistentHistory` from `/api/config`.
- **Triggers group header** — context-aware Pause/Resume buttons (only shows the relevant action based on trigger states) and a `N/M paused` counter when any triggers are paused.
- **Copy key buttons** on jobs and triggers — one-click copy of `group.name` to clipboard.
- **History retrigger** — one-click retrigger button on history rows; reveals on row hover.
- **"due now" pulse** on Next Fire cells when a trigger is due/overdue.
- **Live ticker** — 1-second tick drives countdowns and live durations across the app.
- **`rowDensity` persisted** in the `qd-settings` localStorage bundle alongside `sidebarOpen`, `refreshInterval`, etc.

### Fixed
- **Health nav badge mispositioned** — was absolutely positioned at the far-right of the sidebar button instead of on the icon. Now overlays the icon as a small dot.
- **Triggers group Pause/Resume** — both buttons always showed regardless of state. Now Pause shows only when at least one trigger is running, Resume shows only when at least one is paused.
- **Sparkline trend column never visible** — pre-built `tailwind.css` did not include `xl:table-cell` / `2xl:table-cell` utilities. Added explicit media queries in `responsive.css`.
- **Timeline tooltip "01:00:00 AM" flash** — tooltip was rendering with the zero-epoch initial `timeMs`. Now gated on `timelineCursor.timeMs > 0`.
- **Graph "Current Rate" unit mislabel** — value is averaged executions per 1-minute bucket; label was `/s`. Corrected to `/min`.
- **History trigger column truncated too early** — `max-w` raised from 140px to 200px.
- **Executing empty state emoji** — replaced ⏱️ with an inline SVG play-circle icon for visual consistency.

## [4.0.0] — 2026-05-11

This release splits the package into three:
- **`Dot.QuartzDashboard`** — the dashboard middleware, handlers, in-memory + file history stores.
- **`Dot.QuartzDashboard.Abstractions`** — `IFireHistoryStore` + `FireRecord`. Reference this if you only need to implement a custom store.
- **`Dot.QuartzDashboard.Sqlite`** — SQLite-backed persistent history store. Opt-in.

### ⚠️ Breaking changes

- **`IFireHistoryStore` and `FireRecord` moved** from `QuartzDashboard.Internal` to `QuartzDashboard.Abstractions` (in the new `Dot.QuartzDashboard.Abstractions` package). Update your `using` statements.
- **`QuartzDashboardOptions.PersistHistoryToSqlite` removed.** SQLite persistence now lives in the separate `Dot.QuartzDashboard.Sqlite` package:
  ```csharp
  // before (v3):
  services.AddQuartzDashboard(o => o.PersistHistoryToSqlite = "history.db");

  // after (v4):
  services.AddQuartzDashboard();
  services.AddQuartzDashboardSqliteHistory("history.db");  // call AFTER AddQuartzDashboard
  ```
  Add a `<PackageReference Include="Dot.QuartzDashboard.Sqlite" />`. The main package no longer depends on `Microsoft.Data.Sqlite`.
- **`ConfigHandlers` `hasPersistentHistory`** is now derived from the registered store type (any store other than the in-memory default counts as persistent) rather than reading an options flag.

### Added
- **`IQuartzDashboardOptions`** — read-only contract over `QuartzDashboardOptions`. Handlers and external integrations should depend on this rather than the mutable concrete class. Registered alongside the mutable type in DI.
- **`PagedResponse<T>`**, **`StatusResponse`**, **`ErrorResponse`**, **`FireRecordDto`** response records in `QuartzDashboard.Models`. Wire format is identical to the previous anonymous shape (camelCase; null-valued fields omitted).
- **`ApiRouter`** — declarative route table replaces the 250-line if/else chain in `HandleApi`. Each route is a `(method, pattern, handler)` triple; patterns use `{}` for single-segment wildcards. New routes are one line apiece.

### Internal
- The dashboard SPA assets, in-memory + file history stores, middleware, and SignalR bridge remain in the main `Dot.QuartzDashboard` package.

---

## [3.1.0] — 2026-05-11

### Fixed
- **`ExecutionBucketService` pruning bug** — encoded-minute arithmetic was non-contiguous, so `cutoff = minute - MaxBuckets` deleted the wrong cells across hour/day rollovers. Switched to Unix-epoch minutes (wire format unchanged).
- **API 500 responses no longer leak `Exception.Message`** — internal details (file paths, SQL fragments, etc.) are now logged with a correlation id and the response carries only `{ "error": "Internal server error", "correlationId": "…" }`.
- **`OnAuthorize` callback now applies to SignalR hub** — the `/hub/*` bypass previously ran before auth checks, so `OnAuthorize` was never invoked for hub negotiate. Reordered the middleware so all auth gates apply uniformly.
- **`OnAuthorize` returns 403 when the user is authenticated** (permission denied), 401 only when there is no identity — previously it always returned 401.
- **Stack traces in the job log buffer are no longer truncated to 800 chars** — the bottom frames (often the most diagnostic) were being cut off. Stored in full; the buffer's per-job count cap bounds memory.

### Changed
- **SQLite history store** now enables `PRAGMA journal_mode=WAL` and `synchronous=NORMAL` for concurrent reads and far fewer fsyncs. Added an index on `job_key`. TTL pruning is throttled to once per minute (was running on every read at the 5s dashboard cadence).
- **`IFireHistoryStore` gains filter-pushdown methods** (`GetRecent(count, offset, jobKey)` and `CountFiltered(jobKey)`) with default interface implementations — non-breaking for existing custom stores. The `GET /api/history?job=…` endpoint now pushes the filter down to SQL instead of materializing the full store.
- **`FileFireHistoryStore` debounces writes** — coalesces all `RecordFire` calls inside a 1-second window into a single disk write (was one whole-file write per fire). Flushes synchronously on `Dispose`.
- **`ImportJobs` reflection is cached** — was scanning every loaded assembly's types on every imported job; now memoized in a `ConcurrentDictionary`, invalidated when new assemblies load.
- **Options are validated at registration time** — `AddQuartzDashboard()` now throws `ArgumentException` for empty `Path`, missing leading `/`, negative counts, or non-http(s) `WebhookUrl` (was silently broken at first request).
- **Webhook JSON uses a cached `JsonSerializerOptions`** instead of allocating one per failure.
- **`Quartz` / `Quartz.Extensions.DependencyInjection`** package references now use bracketed ranges `[3.18.0, 4.0.0)` — locks out unverified 4.x without changing the floor.

### Removed
- Dead `RecordExecution` no-op method.
- Duplicate `StringExtensions.Truncate` (one copy retained in `Internal/`).

## [3.0.6] — 2026-05-11

### Changed
- **History**: inline error snippet shown on failed rows without needing to open the modal
- **History**: CSV export button now uses dark-theme styling (`btn-ghost`) instead of hardcoded light colours
- **History**: search filter and date-range filter auto-reset when navigating away from the History page
- **Health**: success rate stat card shows "based on N loaded records" context subtitle
- **Triggers**: removed redundant `(4s)` countdown parenthetical in the Next Fire column — `formatCountdown` already shows "in 4s"
- **Triggers**: action buttons (`Pause`, `Resume`, `Delete`) now `flex-wrap` instead of clipping on narrow viewports
- **Executing**: richer empty state — explains live-connection behaviour and shows an animated green dot
- **Overview**: pin affordance hint shown when no jobs are pinned, guiding users to the Jobs page
- **Overview**: "Manage in Jobs →" link added to the Pinned Jobs section header
- **Jobs**: mobile search toggle button (🔍) in the toolbar reveals/hides the search input on small screens
- **Jobs**: `jobSearchOpen` state flag and `x-ref="jobSearchInput"` for focus management
- **navigateTo**: clears `historyFilterObj.search` and `historyFilterObj.dateRange` when navigating away from the History page

### Fixed
- Removed unused `--dot-size` inline style from the Executing empty state status dot

## [3.0.5] — 2026-05-11

### Fixed
- **Mobile ghost card bug**: group header rows were rendered as full cards on mobile due to `display: block !important` in `.job-group-row td` overriding Alpine's `style="display: none;"` — removed the `!important` flag

### Changed
- **Overview stat cards**: each card is now clickable and navigates to its target page (Jobs, Triggers, Executing, History); added `cursor-pointer`, `role="button"`, and keyboard (`@keydown.enter`) support
- **Overview grid**: changed from `grid-cols-1 sm:grid-cols-2` to `grid-cols-2` — always 2-column on mobile
- **Jobs Last Run**: column now shows relative time (e.g. "3m ago") with the absolute date on hover via `:title`
- **Jobs Actions dropdown**: added "View history" as the first action — navigates to History pre-filtered for that job by setting `historyFilterObj.search`
- **Health Recent Failures**: replaced the redundant "Failed" badge with an inline error message (`h.errorMessage || h.exceptionMessage`); changed from `flex items-center` to `flex items-start` to handle multi-line errors
- **History date-range filters**: added 1h / 6h / 24h / All quick-filter toggle buttons beside the status filter
- **History filter logic**: `filteredHistory` getter applies `historyFilterObj.dateRange` to cut off records older than the selected window
- **Command palette**: job actions renamed from "Trigger job X.Y" → "Run now: X.Y"; added `keywords: ['trigger', 'fire', 'execute', 'start', 'run']` for alias matching; `filteredCommands` now searches against keywords; history results deduplicated by `jobKey` (was one entry per record)
- **`formatDuration`**: added .NET `TimeSpan` string parsing (`[d.]hh:mm:ss[.fffffff]`) — fixes uptime displaying as raw string
- **Mobile bottom nav**: added Triggers and Executing buttons (7 items total); nav is now `overflow-x: auto` with hidden scrollbar and `flex-shrink: 0` items so all pages are reachable
- **`historyFilterObj`**: added `dateRange: 'all'` to initial state
- **`state.js`**: added `dateRange: 'all'` to `historyFilterObj` initial value

## [3.0.4] — 2026-05-11

### CI/CD
- Added **CodeQL Analysis** — security-and-quality scanning on every push and PR
- Added **Release Drafter** — auto-generates release notes from PR labels
- Added **Stale bot** — marks inactive issues/PRs after 60 days
- Added **NuGet cache + npm cache** to all CI jobs (faster builds)
- Fixed CodeQL autobuild — replaced with manual `npm ci` + `dotnet build` (esbuild dependency)

### Community
- Added **MIT LICENSE** file
- Added **Contributor Covenant CODE_OF_CONDUCT**
- Added **FUNDING.yml** (GitHub Sponsors, Ko-fi)
- Added **Issue templates** (Bug Report, Feature Request)
- Added **Discussion templates** (Q&A, Ideas, Show-and-Tell)
- Set NuGet.org homepage and protected `main` branch with required reviews + status checks

### Housekeeping
- Purged `.hermes/` directory from git history via `git-filter-repo`
- Squashed duplicate commits after history rewrite
- Added `.hermes/` to `.gitignore`

## [3.0.3] — 2026-05-10

### CI/CD
- Fixed `Create GitHub Release` in auto-tag publish flow by explicitly setting `tag_name` when run is branch-triggered.
- This unblocks fully automated release pipeline: tests pass -> auto-tag -> NuGet push -> GitHub Release.

## [3.0.2] — 2026-05-10

### CI/CD
- Fixed auto-tag flow so NuGet publish runs in the same successful CI run after tag creation.
- Prevented missed publish when tag is created by `GITHUB_TOKEN` (which does not trigger a second workflow run).

## [3.0.1] — 2026-05-10

### Fixed
- Restored dark-first visual theme variables to prevent broken light/washed-out UI.
- Removed "button inside button" look in Graph toolbar toggle groups.
- Fixed CI packaging reliability by tracking lock/minified assets required by embedded-resource tests.

### CI/CD
- Added **automatic tag creation** after successful `build-and-test`, `demo-build`, and `integration-test`.
- CI now auto-pushes `v<Version>` from `QuartzDashboard.csproj`, which triggers NuGet publish workflow.

## [3.0.0] — 2026-05-10

### ⚠️ Breaking Changes
- Removed `AddQuartzDashboardHistory()` — history is now automatically registered by `AddQuartzDashboard()`
- Removed `UseSystemFonts` option — system fonts are now the default (no embedded fonts)
- Package ID remains `Dot.QuartzDashboard`

### Added
- **Dark mode** — automatic system preference detection + manual toggle, persisted in localStorage
- **SQLite persistent history** — `PersistHistoryToSqlite` option for fire history that survives restarts
- **Next-N-fires preview** — trigger detail shows next 10 scheduled fire times
- **CSV history export** — one-click client-side CSV download of fire history
- **Favicon badge** — browser tab shows red badge with failed job count
- **Job search/filter** — real-time search across job name, group, type, and description
- **esbuild minification pipeline** — JS/CSS assets minified at build time
- **XML documentation** — full IntelliSense support for all public APIs
- **Structured error responses** — API errors return JSON `{ error, type }` instead of raw strings

### Changed
- **Package size reduced ~50%** — 1.7MB → ~860KB (removed fonts, minified assets)
- Frontend split into ES modules (`src/`) bundled by esbuild
- Backend router refactored to dictionary-based dispatch with route constants
- `FileFireHistoryStore` now uses debounced writes (5s) instead of per-event disk I/O
- SignalR bridge uses typed `Channel<DashboardEvent>` with explicit unsubscribe
- All fire-and-forget paths now log warnings via `ILogger`
- `index.html` organized with 29 section markers
- Non-embedded file requests now return 404 instead of SPA fallback

### Fixed
- CS0618 warnings from obsolete `StringKeyDirtyFlagMap.Put()` usage
- Memory leak risk from missing SignalR event unsubscribe in `StopAsync()`
- Silent exception swallowing in webhook, callback, and persistence paths

### Removed
- Embedded Inter and JetBrains Mono fonts (279KB savings)
- Dead `tailwind.css` (empty file)
- Dead `RecordExecution()` stub
- Duplicate `Internal/QuartzDashboardOptions.cs`
- `[Obsolete] AddQuartzDashboardHistory()` method

### Infrastructure
- Added `.editorconfig`, `Directory.Build.props`, `global.json`
- Enabled `GenerateDocumentationFile` for XML doc output
- CI: fixed duplicate integration test runs, added Node.js setup, added package size gate (900KB)

## [2.4.5] — 2025-05-09
- Bug fixes and UI improvements

## [2.4.0] — 2025-05-01
- Embedded assets (fonts, JS, CSS) — zero CDN dependency
- Health monitoring page
- Execution heatmap visualization

## [2.3.2] — 2026-05-09

### Fixed
- **Boot loader actually visible** — Moved boot loader outside `x-cloak` wrapper so it renders immediately on page load (before Alpine.js initializes), giving instant branded feedback instead of a blank screen.
- **README updated** — Added all v2.3.x features: global search, keyboard shortcuts, execution detail drawer, CRON builder, heatmap, embed mode, mobile nav, boot loader, data pulse, and new API endpoints.

## [2.3.1] — 2026-05-09

### Added
- **App boot loader** — Branded splash screen with animated "Q" logo and phase text (Connecting to SignalR → Loading scheduler data → Loading history) while the dashboard initializes. Smooth fade-out when ready.
- **SignalR reconnection toasts** — Visual feedback when connection drops ("Connection lost — reconnecting..."), recovers ("Reconnected via SignalR"), or falls back to polling.
- **Data pulse indicator** — Small indigo dot pulses in the footer status bar when SignalR pushes real-time data, confirming the live connection is actively receiving events.

### Fixed
- **Version display** — About section in Settings now shows the actual Quartz version from the scheduler API instead of hardcoded "2.0.9".

## [2.3.0] — 2026-05-09

### Added
- **Global search** — Press `Ctrl+K` or `/` to open a search overlay that searches across all jobs, triggers, and history in real-time.
- **Keyboard shortcuts** — Press `?` to see all shortcuts. Navigate with `G+J` (Jobs), `G+T` (Triggers), `G+H` (History), etc. Refresh with `R`, fullscreen with `F`.
- **Execution detail drawer** — Click any history row to open a detail panel showing job key, trigger key, duration, fire time, refire count, and full error stacktraces for failed executions.
- **CRON expression builder** — Visual modal for building cron expressions with per-field inputs and quick presets (every minute, hourly, daily, weekdays, etc.).
- **Execution heatmap** — New `/api/heatmap` endpoint + client-side heat grid showing execution density by day-of-week × hour-of-day with success rate color coding.
- **Table column sorting** — Click column headers on Jobs, Triggers, and History tables to sort ascending/descending with visual indicators.
- **Empty state illustrations** — Friendly icons and messages when tables have no data (jobs, triggers, history, executing, calendars).
- **Health notification badge** — Red pulsing dot on the Health sidebar link when success rate drops below 95%.
- **Mobile responsive nav** — Sidebar collapses to a bottom tab bar on screens < 768px wide with touch-friendly targets.
- **Breadcrumb navigation** — Breadcrumb trail in the header showing Dashboard → current page.
- **Embed mode** — Append `?embed=true` to strip sidebar and header for iframe embedding.
- **Multi-scheduler support** — New `/api/schedulers` endpoint; UI ready for scheduler picker dropdown when multiple schedulers are registered.
- **Job dependency graph data** — Infrastructure for visualizing job→trigger relationships.

### Changed
- History table rows are now clickable (cursor pointer) to open execution details.
- Sortable table headers show ▲/▼ indicators with active state highlighting.
- Template count increased from 95/95 to 101/101 (6 new `x-if` templates for overlays).

## [2.2.0] — 2026-05-09

### Added
- **Server-side table pagination** — Jobs, triggers, and history tables now have proper page controls with server-side offset/limit. Current page is preserved during auto-refresh.
- **Integration test project** — 61 new xUnit integration tests via `WebApplicationFactory` covering endpoints, auth, config, coexistence, SignalR, read-only mode, and history tracking. Simulates a real-world API plugging in the NuGet.
- **CI integration tests** — GitHub Actions workflow now runs integration tests as a separate job before publishing.
- **Favicon** — Inline SVG "Q" icon in the browser tab (zero external dependencies).
- **Sticky table headers** — Table headers stay visible when scrolling long lists.
- **Skeleton loading animations** — Shimmer effect for loading states with light mode support.
- **Connection status indicator CSS** — Pulsing dot styles for connected/polling/disconnected states.
- **Smoother page transitions** — Improved enter/leave CSS transitions between dashboard pages.

### Fixed
- **Timeline fills full width** — Removed erroneous 144px offset; SVG now uses 100% of container width with proper ResizeObserver and double-RAF sizing on page change.
- **Light mode contrast** — Comprehensive button, table, input, drawer, badge, tooltip, and pagination overrides for proper readability in light mode. Buttons no longer appear white-on-white.

### Changed
- Pagination state persists during dynamic refresh — page numbers don't reset when auto-refresh fires.
- History live updates no longer corrupt pagination state.

## [2.1.47] — 2026-05-09

### Fixed
- **Trigger accordion state persists on refresh** — Collapsed/expanded state of trigger groups no longer resets when the page auto-refreshes or the user clicks Refresh. State is keyed by stable job key (`Group.Name`) instead of array index, and `loadTriggers()` merges new groups into existing state rather than replacing it.

## [2.1.46] — 2026-05-09

### Changed
- **Timeline limit increased 50→500** — Timeline, history store, and backend all now store up to 500 fire records (was 50 client-side, 100 backend). `MaxFireHistory` option default bumped to 500.
- **Timeline "Fit" button** — New button next to range picker auto-calculates optimal range from data spread.
- **Timeline pulsing "now" marker** — The right-edge "now" indicator now pulses for better visibility.

### Added
- **History page total count** — Header shows "X records" badge and "Showing X–Y of Z" pagination info.
- **Jobs table "Last Run" column** — Shows when each job last executed, derived from fire history.
- **Overview "Last Error" card** — Displays the most recent failure with job name, time, and truncated error message for quick diagnostics.
- **Health thread pool utilization bar** — Visual progress bar showing active threads vs total pool size.
- **Settings retention info** — Displays `MaxFireHistory` and `HistoryRetentionHours` from the running configuration.
- **Execution Graph duration overlay** — Green duration axis on the right side of the graph for avg duration per bucket.

## [2.1.45] — 2026-05-09

### Fixed
- **Timeline auto-fit** — Timeline range now auto-selects the best window (10m/30m/1h/3h) based on the actual data spread on load, so bars fill the full width instead of clustering on the right.
- **Light mode buttons invisible** — Added `.light .btn`, `.light .btn-icon`, `.light .btn-ghost` overrides with visible borders and darker text colors. Buttons no longer blend into white backgrounds.
- **Light mode range picker** — `.bg-gray-900` segment control (used on timeline/graph pages) now renders as light gray in light mode with proper border.
- **Light mode tooltip** — Timeline tooltip overridden from dark to white background with shadow.
- **Light mode badges** — All status badges (running/paused/idle/error) now have explicit text colors alongside background tints for readable contrast.
- **Light mode drawer panels** — Panels using `bg-gray-900/950/800` classes now override to white/slate in light mode.
- **SVG chart override specificity** — `svg text` and `svg line` overrides now use attribute selectors to avoid breaking colored chart elements.

### Improved
- **Light mode color depth** — Borders use 0.08+ opacity (up from 0.06), button backgrounds use 0.05+, and all colored text maps to darker WCAG-AA variants (e.g., emerald-400 → `#047857`, red-400 → `#b91c1c`).

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
