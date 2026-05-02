# QuartzDashboard v1.0 — UI/UX Overhaul + Live Execution Graph

## Goal

Transform the QuartzDashboard from a functional-but-basic SPA into a polished, professional-grade monitoring dashboard with real-time execution visualization — all while keeping the zero-build-step, single-file approach (Alpine.js + Tailwind CDN).

---

## Current State

- **Single HTML SPA** (29KB, 564 lines) at `QuartzDashboard/wwwroot/index.html`
- **5 pages**: Overview, Jobs, Triggers, Executing, History
- **Backend**: 14 REST endpoints in raw middleware at `QuartzDashboardApplicationBuilderExtensions.cs`
- **Fire history**: `ConcurrentQueue<FireRecord>` with max 100 items, recorded via `IJobListener`
- **No graphs/charts** — all data is tabular
- **No polling rate control** — hardcoded 5s refresh
- **No search/filter** — every page shows all data

---

## Proposed Changes

### Phase 1: UI/UX Overhaul (index.html)

#### 1.1 Visual Design Polish

| Change | Detail |
|--------|--------|
| **Sidebar** | Collapsible (hamburger toggle). Icons-only mode when collapsed. Smooth width transition (250ms). |
| **Glassmorphism** | Apply `backdrop-blur-xl bg-gray-800/40` instead of flat `bg-gray-800/60` on cards |
| **Gradient accents** | Subtle gradient borders on active sidebar items and stat cards |
| **Typography** | Slightly larger headers (text-2xl), tighter card padding, better visual hierarchy |
| **Animations** | Page transitions via `x-transition` (fade + slide), staggered stat card entrance |
| **Status indicators** | Replace text badges with colored dots + subtle glow for running/standby/stopped |
| **Responsive** | Sidebar collapses to icons on <1024px, full hamburger menu on <768px |

#### 1.2 Improved Pages

**Overview page:**
- stat cards with mini sparkline (last 10 data points using SVG polyline)
- Scheduler info section with better visual layout (icon per row)
- Quick-action buttons with hover tooltips
- Execution rate display (jobs/min — requires new `/stats` endpoint)

**Jobs page:**
- Expandable rows — click a job to see its triggers inline (no page change)
- Schedule description column (e.g., "Every 30s", "Every 1min" derived from trigger type/data)
- Color-coded status bar on each row edge (green=idle, blue=with triggers, amber=paused, red=error)
- Quick search/filter input at top (filters by name/group/type as you type)

**Triggers page:**
- Grouped by job (accordion: job header → trigger list)
- Visual timeline for next fire time (relative + absolute, e.g., "in 23s · 19:07:00")
- Calendar/CRON expression display for CronTrigger types

**Executing page:**
- Live duration bar (animated horizontal bar showing % of estimated completion)
- Auto-highlight new entries when they appear
- Sound? No — visual pulse only

**History page:**
- Mini bar chart per row showing relative duration (longest bar = full width)
- Filter by job name / outcome
- Pagination (show 20, load more button)

#### 1.3 New: Calendar Page
- List all Quartz calendars (if any)
- Show calendar name, type, description
- Calendar exclusion dates displayed in a mini calendar grid

#### 1.4 New: Settings Panel
- Refresh interval slider (1s – 60s)
- Page auto-refresh toggle per-page
- Theme toggle (dark/light) — currently dark-only

#### 1.5 Alpine.js Architecture Improvements
- Extract `dashboard()` function into separate stateful sub-stores per page (jobsStore, triggersStore, statsStore) using Alpine stores
- Add `x-teleport` for modals (job detail popup, trigger detail)
- Add loading skeletons instead of spinner overlay
- Debounce rapid API calls

### Phase 2: Live Execution Graph

#### 2.1 Backend — New `/api/stats` Endpoint

Add handler in `QuartzDashboardApplicationBuilderExtensions.cs`:

```
GET /api/stats
```

Returns:
```json
{
  "executionRate": 2.5,
  "executionsPerMinute": [
    { "minute": "19:05", "count": 3, "avgDuration": 1.2 },
    { "minute": "19:06", "count": 1, "avgDuration": 0.8 },
    ...
  ],
  "jobBreakdown": [
    { "jobName": "ReminderJob", "totalExecutions": 12, "avgDuration": 0.5, "lastRun": "..." },
    ...
  ],
  "uptime": "2h 34m",
  "totalExecutions": 145
}
```

Maintain an in-memory `ConcurrentQueue<ExecutionBucket>` (max 60 buckets, 1 bucket per minute). Each fire event increments the current minute's bucket.

#### 2.2 Frontend — Live SVG Line Chart

- **No external chart library** — pure SVG `<polyline>` rendered by Alpine
- **View mode toggle**: "Live" (last 5 minutes, 1s refresh) / "5min" (last 30 min) / "Hour" (last 60 min)
- **Chart features**:
  - Grid lines (horizontal, subtle)
  - Y-axis labels (execution count)
  - X-axis labels (minute marks)
  - Tooltip on hover (SVG `<rect>` overlay + positioned text)
  - Smooth line via `<path>` with bezier curves (optional, quadratic)
  - Fill gradient below the line
  - Peak highlight dot on max value
- **Dual line**: execution rate (solid) + average duration (dashed, on secondary Y-axis)

#### 2.3 New "Execution Graph" page

Add to sidebar nav: a graph icon page that shows:
- The live SVG chart (full width)
- Job breakdown table below (sorted by most executions)
- Stats summary row (total, avg duration, peak rate)

### Phase 3: Backend Refinements

| Change | File | Detail |
|--------|------|--------|
| Stats endpoint | `QuartzDashboardApplicationBuilderExtensions.cs` | New `HandleStats` method, new `ExecutionBucket` record |
| History limit | Same | Increase from 100 to 500, add time-based pruning (keep last 1h) |
| Trigger schedule text | `GetAllJobs` / `GetAllTriggers` | Add `scheduleDescription` field: parse SimpleTrigger interval into "Every 30s", CronTrigger into readable expression |
| CORS | Same | Already works, no changes needed |
| Error formatting | `HandleApi` catch block | Return structured error with trace in dev mode |

---

## Files to Change

| File | Changes |
|------|---------|
| `QuartzDashboard/wwwroot/index.html` | Full SPA rewrite (~800 lines, up from 564) |
| `QuartzDashboard/QuartzDashboardApplicationBuilderExtensions.cs` | Add `/api/stats` handler, increase history limit, add schedule description generation |
| `QuartzDashboard/QuartzDashboardOptions.cs` | Add `RefreshIntervalDefault` option (int, seconds) |
| `QuartzDashboard/QuartzDashboardServiceCollectionExtensions.cs` | Move history capacity into options |
| `README.md` | Update with new features |

---

## Tests / Validation

1. **Demo app** (`QuartzDashboard.Demo`): run with 3 demo jobs, verify all endpoints return correct data
2. **Chart test**: verify SVG chart renders correctly at all zoom levels
3. **n8Booking integration**: verify `/quartz/` still works with n8Booking's Blazor WASM fallback
4. **Responsive**: test sidebar collapse at 1024px and 768px breakpoints
5. **Performance**: verify 1s polling on the graph page doesn't overwhelm the backend

---

## Risks & Tradeoffs

| Risk | Mitigation |
|------|-----------|
| **SPA file grows large** (single HTML file approach) | Still under 40KB estimated. If it exceeds 50KB, split into `index.html` + `app.js` as embedded files |
| **SVG chart performance with 60+ data points** | SVG with 60 points is trivial (<1ms render). Cap at 120 points max |
| **Stats endpoint adds memory overhead** | Buckets are tiny (~120 bytes each). 60 buckets = ~7KB. Negligible. |
| **Alpine.js complexity at scale** | The dashboard is a single page app with ~5 views. Alpine is adequate. If it grows beyond 10 views, consider Lit or vanilla web components. |
| **CDN availability** | Tailwind CDN and Alpine CDN are reliable. For offline support, compile Tailwind CSS and bundle Alpine inline (optional future phase). |

---

## Open Questions

1. **Light mode**: Do you want a light theme toggle, or stay dark-only? Light mode doubles the Tailwind color classes.
2. **Persistence**: Should the chart data survive page refresh? (localStorage) or reset on load?
3. **History depth**: 500 events in memory — should we persist to a backing store (SQLite via the NuGet) or keep in-memory for simplicity?
4. **Demo app**: Should the demo app also demonstrate the graph with 7x24 simulated data?

---

## Implementation Order

1. Backend: `/api/stats` endpoint + history capacity increase + schedule description
2. Frontend: UI polish (visual design, collapsible sidebar, animations, responsive)
3. Frontend: Jobs/Triggers page improvements (expandable rows, search, inline details)
4. Frontend: Live execution graph page (SVG chart + stats breakdown)
5. Frontend: History page improvements (bar chart, filter, pagination)
6. Frontend: Settings panel (refresh rate, auto-refresh toggles)
7. Demo app update + README
8. Push to GitHub + n8Booking integration test
