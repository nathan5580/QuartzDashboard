---
title: QuartzDashboard — Full UI/UX Overhaul & Information Density
created: 2026-05-03T00:33:39.242927
status: draft
---

# QuartzDashboard — Full UI/UX Overhaul & Information Density Plan

**Target file:** `/Users/home/RiderProjects/QuartzDashboard/QuartzDashboard/wwwroot/index.html` (~3,500 lines)

**Goal:** Transform every page to show more information at a glance, improve data density without clutter, and raise the UX to Stripe/Linear/Vercel level.

---

## Current State Summary

| Page | Lines | Info Shown | Missing |
|------|-------|-----------|---------|
| Overview | 7.6K | Job/trigger/executing counts, scheduler metadata, 3 stat cards | Execution trends, error rate, last execution, quick actions |
| Jobs | 13.9K | Job list, expand triggers/logs, batch ops, search | Status badges, last/next fire time, group filter, column sorting |
| Triggers | 7.0K | Grouped by job accordion, state, schedule | Calendars, misfire info, total fires, state counts |
| Executing | 6.5K | Currently running jobs, duration bars | Thread usage, estimated completion, abort action |
| History | 6.5K | Filtered table by job key, duration, success/error | Date range picker, success rate %, filter by status |
| Graph | 10.7K | Dual-line SVG chart, 15/30/60m toggles, tooltip | Per-job breakdown, P99/P50 lines, cumulative line, export |
| Timeline | 6.2K | SVG horizontal timeline with dots | Job filter, zoom, color by duration |
| Calendars | 4.6K | List of calendars, create modal, delete | Calendar details, edit, associated triggers |
| Settings | 5.2K | Refresh slider, auto-refresh per page, light mode | History retention, default page, compact mode, theme variants |

---

## Phase 1: Global Structural Improvements

### 1.1 Data Density Toggle ("Comfortable" / "Compact")
- Add `settings.compactMode` (boolean)
- When compact: smaller padding, tighter spacing, smaller font on data cells
- Sidebar gets a toggle icon at the bottom
- Persistent via localStorage
- Files changed: data model, CSS classes, every page template

### 1.2 Footer Status Bar → Info Bar
Current: scheduler status, job/trigger counts, instance ID, connection, last refresh
Upgrade to:
```
[Scheduler: ● Running] [Jobs: 12 (3 paused)] [Triggers: 18 (2 misfired)] [Exec: 0] [Rate: 2.3/min] [● Live] [Last: 2s ago]
```
- Add execution rate
- Add counts with status breakdowns
- Color-coded status dots
- Compact on mobile

### 1.3 Sidebar Page Badges
Show unread counts or status indicators on sidebar nav items:
- Jobs: show paused job count badge
- Triggers: show misfired trigger count badge
- Executing: show running count badge (red pulse when > 0)
- History: show error count badge

### 1.4 Global Error Banner → Toast Queue
Replace the single global error banner with stacked toasts:
- Per-fetch error toasts (auto-dismiss 5s)
- Global errors shown as persistent toast
- Already partially implemented (toastQueue exists) — needs polish

### 1.5 Keyboard Navigation Enhancement
- `Ctrl+J` / `Ctrl+K`: navigate between pages (not just 1-9)
- `Ctrl+N`: create new job (on jobs page)
- `Ctrl+Shift+N`: create new trigger (on triggers page)
- `Escape`: close all modals (already works)
- `?`: show keyboard shortcuts help overlay

### 1.6 Command Palette Expansion
Current: page switching + trigger job actions
Add: "Go to job X", "Pause group Y", "Run trigger Z", filter by status

### 1.7 Auto-Refresh Visual Indicator
When auto-refresh fires, show a subtle flash or shimmer in the sidebar/header
- Especially important on Executing page (need to see it updating live)

---

## Phase 2: Overview Page Redesign

### 2.1 Stat Cards → Mini Dashboard
Current: 3 stat cards (jobs, triggers, executing) + scheduler details table
Upgrade to:

**Row 1 — Key Metrics (6 wide cards):**
```
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Jobs         │ │ Triggers     │ │ Executing    │ │ Exec Rate    │ │ Success Rate │ │ Uptime       │
│ 12           │ │ 18           │ │ 0            │ │ 2.3/min      │ │ 97.8%        │ │ 12h 34m      │
│ 3 paused     │ │ 2 misfired   │ │ ─            │ │ ┌svg spark─┐ │ │ ┌svg spark─┐ │ │ v3.18.0      │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
```

**Row 2 — Scheduler Meta:**
Collapsible detail panel with: name, instance ID, job store, thread pool, scheduler summary, start button

**Row 3 — Recent Activity Feed (new):**
```
[▸ 2s ago] HealthCheck.DEFAULT — OK (15ms)
[▸ 5s ago] CacheWarmup.DEFAULT — OK (2.1s)
[▸ 8s ago] DataSync.DEFAULT — ERROR (Connection timeout)
```
Last 10 fire events, inline with color-coded success/error
This replaces the need to jump to History page for quick glance

### 2.2 Execution Sparkline
Use the existing `executionBuckets` data to draw a small sparkline in the "Exec Rate" card
- Already computing in backend — just render it on overview (not just graph page)

### 2.3 Quick Action Buttons
- "Start Scheduler" / "Standby" (already there)
- "Refresh All"
- "View Running Jobs" → jumps to executing page
- "Create Job" → jumps to jobs page with modal open

---

## Phase 3: Jobs Page — Information-Dense

### 3.1 Row-Level Data — No Expand Needed
Current: job rows show name, group, type, executing status. Triggers hidden behind expand.
Change: Each job row shows:
```
☐ [● Running] HealthCheck            DEFAULT    IJob     Next: in 12s   Last: 2s ago (15ms)   ⏵≡
☐ [● Paused ] ReportGeneration       DEFAULT    IJob     Next: ─        Last: 2m ago (4.2s)   ⏵≡
```
- Status badge (Running/Paused/Error)
- Job name + group
- Job type (short)
- Next fire time (relative, color-coded: green if <1m, amber if <5m)
- Last fire time + duration
- Expand/collapse arrow + actions gear

### 3.2 Status Badges
- **Running**: green dot + "Running" (pulsing if currently executing)
- **Paused**: amber dot + "Paused"
- **Error**: red dot + "Error" (if last execution failed)
- **Durable**: gray dot + "Manual"

### 3.3 Group Filter (New)
Add a dropdown/button group to filter by group:
```
[All] [DEFAULT] [MyGroup] [System]
```
Only shows if more than 1 group exists.

### 3.4 Column Sorting
Click on column headers to sort by: name, group, next fire time, last fire time, status
Add sort indicators (▲/▼)

### 3.5 Bulk Status Bar
Current: batch action bar shows when items selected.
Add: count of selected + "N selected" label + select all checkbox in header

### 3.6 Job Detail Panel (Side Drawer, not Modal)
When expanding a job, instead of inline expansion, slide in a right-side drawer:
- Job metadata (full type, description, JobDataMap)
- Triggers list with state, schedule, next/last fire
- Execution history for this job (last 10 fires)
- Execution logs (already exists)
- Actions: Trigger, Pause, Resume, Delete
- This avoids page reflow from inline expansion

---

## Phase 4: Triggers Page — Grid View

### 4.1 Card Grid Instead of Accordion
Current: grouped by job in an accordion → one job expands, pushing others down
Change to: card grid (3 columns on wide, 2 on medium, 1 on narrow)
```
┌──────────────────────┐ ┌──────────────────────┐ ┌──────────────────────┐
│ HealthCheck-trigger  │ │ DataSync-CRON-trigger│ │ ManualNotification   │
│ Job: HealthCheck     │ │ Job: DataSync        │ │ Job: ManualNotify    │
│ ● Normal             │ │ ● Normal             │ │ ● Paused             │
│ Every 15s            │ │ 0/30 * * * * ?       │ │ ─                    │
│ Next: in 12s         │ │ Next: in 18s         │ │ Next: ─              │
│ Last: 2s ago (15ms)  │ │ Last: 5s ago (1.2s)  │ │ Last: never           │
│ Calendar: ─          │ │ Calendar: ─          │ │ Calendar: ─          │
│ [⟳] [⏸] [▶] [🗑]   │ │ [⟳] [⏸] [▶] [🗑]   │ │ [⟳] [⏸] [▶] [🗑]   │
└──────────────────────┘ └──────────────────────┘ └──────────────────────┘
```
- Shows trigger name, job name, state badge, schedule, next/last fire, calendar, actions
- Actions: Trigger (⟳), Pause (⏸), Resume (▶), Delete (🗑)
- State badge: Normal (green), Paused (amber), Complete (gray), Error (red), Blocked (orange)

### 4.2 Group Header
Show job group as a section header with trigger count:
```
DEFAULT (4 triggers)
[HealthCheck, DataSync, ReportGeneration, ManualNotification]
```

### 4.3 Misfire Instruction Badge
Show small label on triggers that have non-default misfire handling:
```
Misfire: Fire Once Now  (small amber tag)
```

### 4.4 Calendar Association
Show calendar name on trigger cards if set:
```
Calendar: US-Holidays  (small blue tag)
```

### 4.5 State Counts
At top of page:
```
All Triggers · 4 total  ● 3 normal  ● 1 paused  ● 0 misfired
```

---

## Phase 5: Executing Page — Live Dashboard

### 5.1 Thread Pool Gauge
```
Thread Pool:  ●●●●●●●○○○  7/10 threads busy
```
Visual progress bar showing thread pool utilization.
Add sparkline showing thread usage over last 60 seconds.

### 5.2 Job Cards with Progress
Current: flat list with duration bars
Upgrade to cards:
```
┌─────────────────────────────────────────────────────┐
│ ● HealthCheck           Started: 12s ago            │
│   Group: DEFAULT        Duration: ████████░░ 12.3s  │
│   Trigger: manual                                  │
│   Fire ID: abc...123                                │
│   Refire: 0                                         │
│                                                     │
│   [Logs: 3 entries]                                 │
└─────────────────────────────────────────────────────┘
```

### 5.3 Estimated Completion
If the job has a known duration pattern from history, show:
```
Est. remaining: ~2.5s (avg 15s)
```

### 5.4 Abort Action
Add a "Kill" / "Abort" button (calls `Interrupt()` on the job execution)
Only when not read-only. With confirmation modal.

### 5.5 Last N Executions Sparkline
For each executing job, show a mini sparkline of its last 10 execution durations

---

## Phase 6: History Page — Full Analytics

### 6.1 Stats Bar
```
Total: 845 fires  ●  Success: 822 (97.3%)  ●  Error: 23 (2.7%)
Avg Duration: 423ms  ●  P50: 215ms  ●  P99: 3.2s
```

### 6.2 Filters (Top Bar)
```
[Job Name: ████████▏] [Status: All ▼] [Date: From ██ To ██] [⟳]
```
- Free-text job name search (autocomplete from known jobs)
- Status dropdown: All, Success, Error
- Date range picker (from/to date inputs)
- "Clear Filters" button

### 6.3 Duration Distribution Mini-Bar
Above the table, show a horizontal bar chart of duration buckets:
```
<10ms:   ████████████████▏ 342
10-50ms: ████████▏ 189
50-200ms:█████▏ 120
200ms+:  ████▏ 68
```

### 6.4 Table Enhancements
Add columns:
- Relative time ("2s ago", "5m ago") as primary — absolute time in tooltip
- Duration with visual bar (already partly there)
- Success/Error with icon (checkmark / X)
- Job name as link → switch to Jobs page and select that job

### 6.5 Pagination
Current: Load More button. Add:
- Page number display ("Page 3 of 17")
- Go to page input
- Results per page selector (25/50/100/200)

---

## Phase 7: Graph Page — Multi-Dimensional

### 7.1 Time Range: 5m, 15m, 30m, 1h, 6h
Add 5m and 6h options to the existing 15/30/60m toggles.

### 7.2 Multi-Line Chart
Current: dual-line (exec count + avg duration)
Add toggleable lines:
- ✅ Execution count (primary)
- ✅ Avg duration (primary)
- ⬜ Error rate (shaded area — already done)
- ⬜ P50 duration (dashed)
- ⬜ P99 duration (dotted)
- ⬜ Cumulative executions (stepped line)

### 7.3 Per-Job Breakdown
Add a dropdown: "All Jobs" | "HealthCheck" | "DataSync" | ...
When a job is selected, the chart shows ONLY that job's metrics.
When "All Jobs", show aggregated as today.

### 7.4 Legend
Inline legend below chart with clickable items to toggle lines:
```
● Exec Count  ● Avg Duration  ● Error Rate  ● P50  ● P99
[☐ Cumulative]
```

### 7.5 Peak Markers
Automatically detect and annotate peaks in execution volume:
```
Peak: 45/min at 14:23 — 2x baseline
```

### 7.6 Export
- "Export as PNG" button → renders chart to canvas via `html2canvas` or native `dom-to-image`
- "Export Data CSV" → downloads execution bucket data as CSV

---

## Phase 8: Timeline Page — Filtered & Focused

### 8.1 Job Filter
Dropdown to filter timeline to a single job's events.

### 8.2 Color Coding by Duration
Timeline dots colored by speed:
- Green: < 100ms
- Amber: 100ms - 1s
- Red: > 1s
- Gray: error

### 8.3 Time Compression
If many events in a short period, compress them visually:
Show event density as a heat bar at the top, click to expand that region

### 8.4 Click to Jump
Click a timeline event → switch to History page filtered to that job + time range

---

## Phase 9: Calendars Page — Feature Complete

### 9.1 Calendar Detail View
Click a calendar → expand to show:
- Type description (e.g. "HolidayCalendar: excludes weekends and US federal holidays")
- Base calendar (if set)
- Creation date
- Associated triggers (queries GET /api/triggers for matching calendar name)
- Excluded dates (for HolidayCalendar)

### 9.2 Calendar Edit
Add edit capability — currently create + delete only.
PATCH endpoint + edit modal pre-filled with current values.

### 9.3 Calendar Preview
For HolidayCalendar/MonthlyCalendar: show an inline mini-calendar with excluded dates marked in red.

### 9.4 Trigger Count Badge
Next to each calendar name: "3 triggers using this calendar"

---

## Phase 10: Settings Page — Power User

### 10.1 Display Settings
- Compact mode toggle (already planned in 1.1)
- Font size: Small / Normal / Large
- Theme: Dark / Light / System (follow OS preference)
- Show execution times in: Relative / Absolute / Both
- Date format: 12h / 24h

### 10.2 Dashboard Defaults
- Default page on load: dropdown (overview/jobs/triggers/etc.)
- Auto-refresh on load: toggle
- Items per page: 25/50/100/200

### 10.3 Data Management
- Clear fire history button (calls new DELETE endpoint)
- Reset all settings to defaults
- Export settings as JSON
- Import settings from JSON

### 10.4 About Section
- Version from scheduler meta (already shown)
- NuGet package version (injected at build time)
- Links: GitHub, NuGet, docs
- "Check for updates" link → opens GitHub releases

---

## Implementation Order

| Phase | What | Effort | Impact | Priority |
|-------|------|--------|--------|----------|
| 1 | Global: density toggle, footer bar, sidebar badges, keyboard shortcuts | 3h | Medium | P1 |
| 2 | Overview: 6 metric cards, sparkline, activity feed | 3h | High | P1 |
| 3 | Jobs: row-level data, status badges, group filter, sort, side drawer | 4h | High | P1 |
| 4 | Triggers: card grid, misfire badges, calendar tags | 3h | High | P1 |
| 5 | Executing: thread gauge, progress cards, abort, completion estimate | 2h | Medium | P2 |
| 6 | History: analytics bar, filters, duration distribution, pagination | 3h | Medium | P2 |
| 7 | Graph: more time ranges, per-job, legend, export | 3h | Medium | P2 |
| 8 | Timeline: job filter, color coding, time compression | 2h | Low | P3 |
| 9 | Calendars: detail view, edit, preview, trigger badges | 2h | Low | P3 |
| 10 | Settings: power user options, data management, about | 2h | Low | P3 |

**Total:** ~27 hours for all phases (6-7 focused sessions)

## Files Changed
- `/Users/home/RiderProjects/QuartzDashboard/QuartzDashboard/wwwroot/index.html` — all SPA changes
- `/Users/home/RiderProjects/QuartzDashboard/QuartzDashboard/QuartzDashboardApplicationBuilderExtensions.cs` — if new API endpoints needed (history DELETE, calendar PATCH)
- `/Users/home/RiderProjects/QuartzDashboard/QuartzDashboard/QuartzDashboardOptions.cs` — if new options

## Validation
1. Each page loads and shows all data fields as described
2. Compact mode: spacing is ~60% of comfortable, no overlap
3. Resize browser: cards reflow, filters stay usable
4. Click all buttons, verify API calls succeed
5. Light mode: all new elements have light mode styling
6. Mobile: all new cards collapse to single column
7. Keyboard shortcuts work as documented
8. Empty states (no data): show helpful messages, not broken layouts

## Open Questions
- Compact mode: should we ship it as default or opt-in?
- Executing page: abort action requires Quartz `Interrupt()` — confirm API works
- Per-job graph: backend needs to return per-job stats, not just aggregate
- CSV export: should we use a new API endpoint or generate client-side?
- Calendar edit: needs PATCH endpoint on backend — what fields to support?
