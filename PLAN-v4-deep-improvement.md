# QuartzDashboard v4 — Deep Improvement Plan

> Current: v3.0.0 (committed). Backend refactored into 20 clean files. SPA is a single 2910-line Alpine.js file with basic SVG charts. No tests.

---

## Theme: Make it the best Quartz dashboard in the ecosystem

Every aspect must feel like a **professional monitoring tool** — Grafana, Datadog, Linear quality. Not a side-project dashboard.

---

## 1. Charts & Visualization (Top Priority)

The user called out charts directly. Current implementation is `<polyline>` with raw SVG points — no curves, no gradients, no axis, no animation, no interactivity.

### 1.1 Chart Engine — `ChartEngine` module

Create a reusable SVG chart renderer inside the dashboard. All inline SVG, zero libraries.

```javascript
const ChartEngine = {
  // Natural cubic spline through data points (monotone, no overshoot)
  smoothPath(points, field, width, height, margin) {
    // Returns SVG path string with C commands — buttery smooth
    // Uses Fritsch-Carlson monotone spline interpolation
  },
  
  // Gradient definitions
  gradients: {
    count: { id: 'countGrad', top: 'rgba(99,102,241,0.15)', bottom: 'rgba(99,102,241,0)' },
    duration: { id: 'durationGrad', top: 'rgba(52,211,153,0.15)', bottom: 'rgba(52,211,153,0)' },
    error: { id: 'errorGrad', top: 'rgba(239,68,68,0.15)', bottom: 'rgba(239,68,68,0)' },
  },
  
  // Y-axis with 4-5 nicely rounded tick values
  yAxisTicks(max, count = 5) {
    // Returns [{value, label, y}] where labels are human-readable
  },
  
  // X-axis time labels
  xAxisTicks(data, timeRange) {
    // Returns [{time, label, x}] — "10:00", "10:05", etc.
  },
  
  // Grid lines (horizontal, dashed)
  gridLines(ticks, width) {
    // Returns SVG line elements
  },
  
  // Vertical crosshair for hover
  crosshair(x, height) {
    // Returns SVG line + circle at intersection
  },
  
  // Export SVG to PNG
  exportPNG(svgElement, filename) {
    // Uses XMLSerializer + Canvas API
  },
};
```

### 1.2 Chart Types

| Type | Render | Use Case |
|------|--------|----------|
| **Smooth Line** | `<path>` with C-curves + gradient fill | Execution count over time |
| **Smooth Area** | Same + fill under line | Volume + trend |
| **Bar** | `<rect>` elements with height animation | Per-minute breakdown |
| **Heatmap** | Grid of colored `<rect>` | Day/hour distribution |
| **Gauge** | SVG arc (donut) | Thread pool utilization |
| **Sparkline** | Mini smooth path + glow | Stat cards |
| **Timeline** | Positioned circles + "now" line | Live event stream |

### 1.3 Interaction

- **Hover**: Vertical crosshair follows cursor, tooltip card shows at cursor position
- **Click**: Click on data point → navigate to related page (e.g., executing jobs at that time)
- **Drag to zoom**: Click-drag on chart → zooms into selected time range
- **Double-click**: Reset zoom to full range
- **Legend click**: Click legend items to toggle series on/off (with animation)
- **Scroll**: Mouse wheel zooms time range in/out

### 1.4 Visual Design

- Smooth 300ms CSS transitions on path data changes
- Gradient fill under area charts (indigo for count, emerald for duration, red for errors)
- Glow filter on sparklines (`feGaussianBlur` + `feMerge`)
- Axis labels in `font-mono` with `text-gray-500` color
- Grid lines: ultra-thin `rgba(255,255,255,0.04)` dashed
- Tooltip: frosted glass effect (`backdrop-filter: blur(8px)`)
- Chart card: `.card-gradient` with subtle top border

---

## 2. Architecture — SPA Modularization

### 2.1 Problem

Single 2910-line file. Even with Alpine.js, this is approaching the maintainability wall. Adding the v4 features will push it past 5000 lines.

### 2.2 Solution: Component-based SPA (no build step)

Split into embedded resource files served by the existing middleware:

```
wwwroot/
├── index.html         # Thin shell (~50 lines, loads CSS + JS + Alpine)
├── app.css            # All styles (can be heavily compressed)
├── app.js             # Core: dashboard() Alpine component (~1500 lines)
├── charts.js          # ChartEngine: SVG renderers, splines, gradients
├── pages.js           # Page templates (health, jobs, triggers, etc.)
├── utils.js           # Helpers: debounce, formatDuration, relativeTime, fetchApi
└── signalr.js         # SignalR connection handler + batch processor
```

The middleware already serves static files from embedded resources — just need to add `<script src="app.js">` etc. to `index.html`.

### 2.3 Backend Improvements

| Area | Issue | Fix |
|------|-------|-----|
| API versioning | No version prefix | Add `/api/v1/` alongside `/api/` |
| OpenAPI docs | No swagger | Add XML comments + OpenAPI description generator |
| Error responses | Inconsistent format | Standardize: `{error, code, detail}` |
| Rate limiting | No protection | Add `UseRateLimiter()` option |
| CORS | Assumes same-origin | Add configurable CORS policy |
| Health endpoint | No dedicated health | Add `GET /api/health` with scheduler status + uptime |
| Metrics endpoint | No Prometheus | Add `GET /api/metrics` with prometheus-formatted stats |
| Test coverage | Zero tests | Add xUnit project with integration tests for all handlers |

---

## 3. Design System

### 3.1 Design Tokens

Centralize all visual properties into CSS custom properties:

```css
:root {
  /* Colors */
  --color-accent: #6366f1;
  --color-accent-hover: #818cf8;
  --color-bg: #030712;
  --color-surface: rgba(17,24,39,0.8);
  --color-border: rgba(255,255,255,0.06);
  --color-text-primary: #f3f4f6;
  --color-text-secondary: #9ca3af;
  --color-text-muted: #6b7280;
  
  /* Spacing */
  --spacing-xs: 0.25rem;
  --spacing-sm: 0.5rem;
  --spacing-md: 1rem;
  --spacing-lg: 1.5rem;
  --spacing-xl: 2rem;
  
  /* Typography */
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
  --font-mono: 'JetBrains Mono', monospace;
  --font-size-xs: 0.75rem;
  --font-size-sm: 0.875rem;
  --font-size-base: 1rem;
  
  /* Border radius */
  --radius-sm: 0.375rem;
  --radius-md: 0.5rem;
  --radius-lg: 0.75rem;
  --radius-xl: 1rem;
  
  /* Shadows */
  --shadow-sm: 0 1px 2px rgba(0,0,0,0.3);
  --shadow-md: 0 4px 20px rgba(0,0,0,0.3);
  --shadow-lg: 0 8px 40px rgba(0,0,0,0.4);
  
  /* Transitions */
  --transition-fast: 0.15s ease;
  --transition-normal: 0.3s ease;
}
```

### 3.2 Component Library

Reusable Alpine components with `x-data`:

```html
<!-- Card component -->
<div x-data="{ hovered: false }" 
     @mouseenter="hovered = true" @mouseleave="hovered = false"
     :class="['card-gradient', hovered ? 'shadow-md translate-y-[-0.5px]' : '']">
</div>

<!-- Modal component -->
<template x-teleport="body">
  <div x-data="{ open: false }" x-show="open" class="modal-overlay"
       x-transition:enter="transition ease-out duration-200"
       x-transition:enter-start="opacity-0"
       x-transition:enter-end="opacity-100">
  </div>
</template>

<!-- Toast component -->
<template x-for="t in toastQueue" :key="t.id">
  <div x-data="{ show: false }" x-init="setTimeout(() => show = true, 10)"
       x-show="show"
       x-transition:enter="transform transition ease-out duration-300"
       x-transition:enter-start="translate-x-full opacity-0"
       x-transition:enter-end="translate-x-0 opacity-100">
  </div>
</template>
```

### 3.3 Micro-interactions Checklist

| Element | Hover | Active | Focus | Transition |
|---------|-------|--------|-------|------------|
| Button | `bg-opacity` increase | `scale(0.97)` | Ring | 150ms ease |
| Card | `translateY(-0.5px)` + shadow | — | — | 150ms ease |
| Sidebar item | `bg-opacity` increase | — | — | 100ms ease |
| Table row | `bg-opacity` increase | — | — | 100ms ease |
| Input/Select | Border highlight | — | Focus ring | 150ms ease |
| Badge | — | — | — | — |
| Modal | Backdrop fade + scale-in | — | — | 200ms ease |
| Toast | Slide-in from right | — | — | 300ms ease |
| Toggle | Knob slide | — | — | 200ms ease |
| Stat card | Subtle lift | — | — | 150ms ease |

---

## 4. UI/UX Overhaul

### 4.1 Page Architecture

```
/quartz/
├── overview/       # Landing: 6 stat cards + scheduler details + activity feed
├── jobs/           # Table: sort, filter, batch select, expandable rows
├── triggers/       # Card grid: state badges, misfire/calendar info
├── executing/      # Live: running jobs with duration bars + abort
├── history/        # Table: filter by job/status, pagination
├── health/         # NEW: failure rate, misfire count, thread pool gauge
├── graph/          # Charts: smooth line/bar/heatmap with interaction
├── timeline/       # Live event stream with zoom/pan
├── calendars/      # Calendar CRUD with trigger associations
├── settings/       # Preferences, connection info, about
└── search/         # NEW: full-text search across all resources
```

### 4.2 Navigation

- **Sidebar**: Current 9 items + health + search. Collapsible. Badges on executing/errors.
- **Command palette**: Ctrl+K — search everything. Already exists but needs enhancement.
- **Breadcrumbs**: Show current location. Click to go up.
- **Quick actions**: FAB (floating action button) for "Create Job" / "Create Trigger"
- **Keyboard navigation**: All pages accessible via keyboard. Arrow keys in tables.

### 4.3 Empty States

Every page needs a thoughtful empty state:

| Page | Empty State |
|------|-------------|
| Jobs | "No jobs registered. Create your first job to get started." + CTA button |
| Triggers | "No triggers configured. Triggers tell Quartz when to fire jobs." + CTA |
| History | "No executions yet. History appears here as jobs run." |
| Health | "Not enough data. Let the scheduler run for a few minutes." |
| Calendars | "No calendars. Calendars exclude certain dates from triggering." |
| Timeline | "Waiting for events. The timeline updates in real-time." |

Each with a relevant SVG illustration (inline, no external assets).

### 4.4 Loading States

| State | Visual |
|-------|--------|
| Initial page load | Skeleton shimmer (pulsing gray blocks matching content shape) |
| Data refresh | Subtle spinner in header, content stays visible |
| Action (trigger/pause) | Spinner replaces icon inside the button, button disabled |
| Batch operation | Progress bar: "3 of 12 jobs processed..." |
| Image/SPA load | Splash screen with animated logo (already exists, refine) |

### 4.5 Error States

| Scenario | Response |
|----------|----------|
| API down | Banner: "Dashboard unreachable. Retrying..." + auto-retry countdown |
| Single fetch fails | Toast: "Failed to load jobs" + retry button for that section |
| Auth fails | Redirect to login / show "Not authenticated" message |
| Scheduler not found | Show "No Quartz scheduler detected" with setup instructions |
| Rate limited | Toast: "Too many requests. Waiting..." with countdown |

### 4.6 Mobile Responsiveness

| Breakpoint | Changes |
|------------|---------|
| >1024px | Full sidebar + content |
| 768-1024px | Collapsed sidebar (icons only) + content |
| <768px | Bottom navigation bar (5 items), full-width content |
| <480px | Single column, stacked cards, smaller text |

---

## 5. Feature Additions

### 5.1 Job Dependency Graph

Visualize job chains as a directed graph:
- Nodes = jobs, sized by execution frequency
- Edges = trigger chains (job A triggers job B)
- Uses SVG `<path>` with arrow markers
- Click node → navigate to job detail
- Zoom/pan via mouse wheel + drag

### 5.2 Schedule Preview

When viewing a trigger, show a calendar preview:
- Next 30 fire times highlighted on a mini month calendar
- Color-coded: green = normal, yellow = misfired, red = skipped
- Hover shows exact date/time

### 5.3 Execution Waterfall

For the executing jobs page:
- Horizontal bar chart showing each job's current execution time
- Color-coded by duration: green (<100ms), amber (<1s), red (>1s)
- "Now" indicator line
- Click row → navigate to job detail

### 5.4 Alerting Rules UI

Configuration panel for alerts:
- Condition: "When job X fails" / "When execution takes > Y seconds"
- Action: Browser notification / Webhook POST / Sound
- List of configured rules with enable/disable toggle

### 5.5 Webhook Configuration

In Settings:
- Register webhook URLs for job events
- Test button sends a sample payload
- List of recent webhook deliveries with status codes
- Secret signing for payload verification

### 5.6 Scheduler Backup/Restore

- Export all job/trigger/calendar definitions as JSON
- Import from JSON to restore state
- One-click backup to localStorage

---

## 6. Performance

### 6.1 SPA Loading

| Optimization | Impact |
|-------------|--------|
| Lazy-load page templates | 40% fewer initial bytes |
| Defer non-critical scripts | Faster time-to-interactive |
| Compress SVG icons | 60% smaller (inline strings vs elements) |
| Remove unused CSS | 30% smaller stylesheet |
| Preload SignalR script | Faster connection establishment |

### 6.2 Runtime

| Optimization | Impact |
|-------------|--------|
| Debounce API calls (done) | Prevents flood on rapid clicks |
| Throttle timeline ticker | Reduces re-renders from 1s to 3s |
| Batch SignalR events (done) | 10x fewer messages at high throughput |
| Virtual scroll for history | Handles 10K+ records |
| Memoize computed properties | Alpine $skip computed when deps unchanged |

### 6.3 Backend

| Optimization | Impact |
|-------------|--------|
| Response compression | 70% smaller payloads |
| ETag + conditional GET | 304 responses for unchanged data |
| Query result caching | 100ms → 2ms for job lists |
| Connection pooling | Fewer SQL connections |

---

## 7. Implementation Phases

| Phase | Focus | Items | Est. Effort |
|-------|-------|-------|-------------|
| **P0** | Charts | ChartEngine, smooth curves, gradients, axis, tooltips, sparkline glow | 2d |
| **P1** | SPA split | Modularize into app.js, charts.js, pages.js, utils.js | 1d |
| **P2** | Design system | CSS custom properties, component library, micro-interactions | 1d |
| **P3** | UI/UX | Empty states, error recovery, mobile, keyboard nav, loading skeletons | 2d |
| **P4** | Charts v2 | Bar chart, heatmap, PNG export, drag-to-zoom, cumulative overlay | 1.5d |
| **P5** | Features | Job dependency graph, schedule preview, execution waterfall, alerting | 2d |
| **P6** | Backend | API versioning, OpenAPI, tests, rate limiting, health/metrics endpoints | 1.5d |
| **P7** | Performance | Lazy loading, virtual scroll, response compression, caching | 1d |

---

## 8. Inspiration

Visual references for the design direction:

| Source | Element to steal |
|--------|-----------------|
| **Grafana** | Chart tooltip with crosshair, time range selector, dashboard grid layout |
| **Datadog** | Sparklines with gradient fill, hover highlights, correlation lines |
| **Linear** | Clean typography, subtle micro-interactions, command palette |
| **Vercel** | Dark theme colors, card design, loading skeletons |
| **Stripe** | Status badges, data table design, empty state illustrations |
| **Linear** | Keyboard shortcuts sheet, modal design, settings page |

---

## 9. What v4.0.0 Will Feel Like

When you open `localhost:5190/quartz`:

```
→ Instant load (no flash of unstyled content)
→ 6 stat cards with animated sparklines that glow subtly
→ Smooth line chart showing execution rate with gradient fill
→ Hover over chart → crosshair follows cursor, tooltip shows exact values
→ Press Ctrl+K → search anything (jobs, triggers, pages)
→ Timeline shows real-time events with smooth entry animation
→ Sidebar badges show live counts with pulse animation
→ Every page has a thoughtful empty state with icon
→ Mobile: bottom nav bar, full-width cards
→ Dark mode toggle transitions smoothly
→ Loading: shimmer skeletons that match content shape
→ Errors: auto-retry with countdown, never a blank page
→ Charts exportable as PNG with one click
→ Job detail modal with 5 tabs (metadata, data map, history, triggers, export)
→ Trigger creation with inline cron tester + next 5 fire times
→ 0 errors, 0 warnings on build
→ CI test suite with 100+ tests passing
```
