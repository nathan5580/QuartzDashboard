# QuartzDashboard v2.0 — Real-Time SignalR Upgrade + Full Quartz Configuration

## Goal

Transform the dashboard from a polling-based SPA into a real-time, SignalR-powered monitoring and configuration interface. Every job execution, trigger fire, scheduler state change pushes instantly to the browser. Add full Quartz.NET configuration capabilities (create/edit/delete jobs, triggers, schedules) through a clean UI.

---

## Current Architecture

### Frontend
- **Single HTML SPA** (1,706 lines, 86KB) at `wwwroot/index.html`
- **Alpine.js 3.x** + **Tailwind CSS v4** via CDN
- **Polling**: `setInterval(refreshAll, 5000)` — fetches all data every N seconds
- **7 pages**: Overview, Jobs, Triggers, Executing, History, Graph, Settings

### Backend
- **Raw middleware** via `app.Map()` in `QuartzDashboardApplicationBuilderExtensions.cs`
- **14 REST endpoints** at `/quartz/api/*`
- **In-memory state**: `ConcurrentQueue<FireRecord>` (100 items), `ConcurrentQueue<ExecutionBucket>` (120 buckets)
- **Fire listener**: `DashboardJobListener` attached via `DashboardListenerAttacher` hosted service
- **No WebSocket/SignalR** — all client-server communication is HTTP polling

---

## Proposed Changes

### Phase 1: Backend — SignalR Hub + Event Bus

#### 1.1 Add SignalR Hub

**New file**: `QuartzDashboard/SignalR/QuartzDashboardHub.cs`

```csharp
public class QuartzDashboardHub : Hub
{
    // Client calls this on connect to register for updates
    public async Task Subscribe() =>
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
    
    public async Task Unsubscribe() =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
}
```

A single hub at `/quartz/hub` (configurable via options).

#### 1.2 Add Event Bus / Dispatcher

**New file**: `QuartzDashboard/Internal/DashboardEventBus.cs`

An in-memory event bus that decouples producers (job listeners, scheduler listeners) from consumers (SignalR hub):

```csharp
public sealed class DashboardEventBus
{
    // Events:
    // JobExecuted(string jobKey, string triggerKey, TimeSpan duration, bool success)
    // JobTriggered(string jobKey, string triggerKey)
    // SchedulerStatusChanged(bool isStarted, bool isStandbyMode)
    // JobDataChanged()  // job/trigger created, deleted, paused, resumed
    
    public event Action<JobExecutedEvent>? OnJobExecuted;
    public event Action<SchedulerStatusEvent>? OnSchedulerStatusChanged;
    // etc.
    
    public void Publish(IEvent @event) { ... }
}
```

#### 1.3 Add SignalR Bridge Hosted Service

**New file or extend**: `QuartzDashboard/Internal/DashboardSignalRBridge.cs`

A background service that subscribes to `DashboardEventBus` and forwards events to the SignalR hub:

```csharp
internal sealed class DashboardSignalRBridge : IHostedService
{
    // Subscribes to DashboardEventBus
    // On each event: calls hubContext.Clients.Group("dashboard").SendAsync("eventName", data)
    // Events: jobExecuted, schedulerStatus, jobTriggered, jobsUpdated
}
```

#### 1.4 Update Fire Listener to Publish Events

Modify `DashboardListenerAttacher` / `DashboardJobListener` to publish to `DashboardEventBus` instead of directly updating static `ConcurrentQueue`s. The queues remain but are updated by the event bus subscribers.

#### 1.5 Add Scheduler Listener

Register an `ISchedulerListener` alongside the job listener that publishes:
- `SchedulerStarted` / `SchedulerInStandbyMode` / `SchedulerShutdown`
- `JobAdded` / `JobDeleted` / `JobPaused` / `JobResumed`
- `TriggerAdded` / `TriggerDeleted` / `TriggerPaused` / `TriggerResumed`

#### 1.6 New REST Endpoints (for full configuration)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/jobs` | Create a new job (body: group, name, jobType, durability, description) |
| DELETE | `/api/jobs/{group}/{name}` | Delete a job |
| POST | `/api/triggers` | Create a new trigger for a job |
| DELETE | `/api/triggers/{group}/{name}` | Delete a trigger |
| POST | `/api/calendars` | Add a calendar |
| GET | `/api/calendars` | List calendars |
| PUT | `/api/scheduler/jobstore` | Update scheduler config (if using RAM store, can't persist) |

#### 1.7 Update Service Registration

`AddQuartzDashboard()` needs to:
- Register `DashboardEventBus` (singleton)
- Register `DashboardSignalRBridge` (hosted service) — only if `UseSignalR` option is true (default: true)
- Add `Microsoft.AspNetCore.SignalR` to dependencies (or make it optional)
- Call `services.AddSignalR()` if not already registered (use `TryAdd` to avoid conflicts)

**New option** in `QuartzDashboardOptions.cs`:
```csharp
public bool UseSignalR { get; set; } = true;  // Default: on
```

#### 1.8 Update Middleware Registration

`UseQuartzDashboard()` needs to map the SignalR hub:
```csharp
app.MapHub<QuartzDashboardHub>($"{basePath}/hub");
```

### Phase 2: Frontend — Real-Time SignalR Client

#### 2.1 Add SignalR JS Client

The SPA currently uses only Alpine.js + Tailwind CDN. Add the SignalR JS client via CDN:

```html
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8/dist/browser/signalr.min.js"></script>
```

(~30KB gzipped ~10KB — acceptable for a monitoring dashboard)

#### 2.2 Replace Polling with Event-Driven Updates

Remove the `setInterval(refreshAll, 5000)` polling. Instead:

```javascript
connection = new signalR.HubConnectionBuilder()
    .withUrl("/quartz/hub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

connection.on("jobExecuted", (data) => {
    // Update executing jobs list
    // Add to fire history
    // Increment execution bucket
    // Update stat cards
    // Update graph data point
    // Show toast if user is on relevant page
});

connection.on("schedulerStatus", (data) => {
    this.scheduler.isStarted = data.isStarted;
    this.scheduler.isStandbyMode = data.isStandbyMode;
});

connection.on("jobsUpdated", () => {
    // Background refresh of jobs/triggers lists
    loadJobs();
    loadTriggers();
});

await connection.start();
await connection.invoke("Subscribe");
```

#### 2.3 Keep Fallback Polling

As a fallback for when SignalR disconnects or fails to connect:
- On disconnect: show a subtle "disconnected" indicator in the footer
- Auto-reconnect via `withAutomaticReconnect`
- If reconnection fails after 3 retries: fall back to polling at the configured interval
- When reconnected: full data refresh + resume event-driven mode

#### 2.4 Optimistic UI Updates

For user actions (trigger job, pause, resume):
- Immediately update the UI state before the API call completes
- Show spinner on the specific button (not full-page loading)
- On API success: show brief toast
- On API failure: revert the optimistic update + show error toast

### Phase 3: Timeline View (New Page)

#### 3.1 "Timeline" Page Design

A new sidebar nav item between "Triggers" and "Executing":

```
[Timeline] — Visual timeline of all job activity
```

**Layout**:
- Horizontal timeline (scrollable) showing the last 5 minutes
- Each job is a row on the Y-axis
- Time flows left-to-right on the X-axis
- Each execution is a colored dot/bar positioned at its fire time
- Hover over a dot: tooltip showing job name, trigger, duration
- Click a dot: jump to History with that job pre-filtered

**Data source**:
- `/api/timeline` new endpoint returning last N fire events with precise timestamps
- Or reuse `/api/history` data, formatted for timeline rendering

**Rendering**:
- Pure SVG, no canvas
- Vertical grid lines every 10 seconds
- Current time indicator line (flashing, moving)
- Job rows with labels on the left
- Color coding: green = OK, yellow = slow (>1s), red = error/failure

#### 3.2 Real-Time Timeline Updates

- When a `jobExecuted` event arrives via SignalR:
  - Add dot to timeline at the correct position
  - Auto-scroll to keep "now" visible
  - Animate new dot appearing (scale 0→1)

### Phase 4: Full Quartz Configuration UI

#### 4.1 Job Creation Modal

New button on Jobs page: "Create Job" opens a modal with:

**Fields**:
- Job Name (required, text)
- Job Group (text, default "DEFAULT")
- Description (text)
- Job Type (dropdown or text — .NET type name for `IJob` implementation)
- Durable (toggle — store without trigger)
- Concurrent Execution Disallowed (toggle — `[DisallowConcurrentExecution]`)
- Persist Job Data After Execution (toggle)

**On submit**: POST `/quartz/api/jobs` → SignalR broadcasts `jobsUpdated`

#### 4.2 Job Detail / Edit Modal

Click a job's expand chevron → inline detail panel (current behavior).
NEW: "Edit" button opens a modal showing:
- Job metadata (read-only fields)
- JobDataMap (key-value editor — add/remove/edit entries)
- Triggers list with individual edit/delete controls
- "Delete Job" button (with confirmation)

#### 4.3 Trigger Creation Modal

From Jobs page, "Add Trigger" button (in expanded details):

**Trigger Type** (radio/tabs):
- Simple Trigger: repeat interval (number + unit: seconds/minutes/hours), repeat count (∞ or number)
- Cron Trigger: CRON expression (text input + preview of next 5 fire times)
- Daily Time Interval Trigger: interval, days of week checkboxes

**Fields (all types)**:
- Trigger Name (required)
- Trigger Group (default "DEFAULT")
- Description
- Start Time (datetime picker)
- End Time (optional datetime picker)
- Priority
- Calendar Name (dropdown of existing calendars)

**On submit**: POST `/quartz/api/triggers` → SignalR broadcasts `jobsUpdated`

#### 4.4 Trigger Edit Modal

From Triggers page or Jobs > Triggers inline, click "Edit":
- Modify schedule parameters (if mutable for the trigger type)
- Change priority
- Change calendar association
- "Delete Trigger" button

#### 4.5 Calendar Management

New "Calendars" page (optional sidebar item) or sub-section of Settings:
- List existing calendars
- Add calendar: HolidayCalendar, AnnualCalendar, MonthlyCalendar, WeeklyCalendar, DailyCalendar
- Calendar exclusion days picker (date picker or CRON-like)
- No Calendar support in base Quartz for all operations, but basic display + add/remove

### Phase 5: Real-Time Graph Overhaul

#### 5.1 Streaming Graph

Current graph polls `/api/stats` every N seconds. Replace with:

- SignalR event `executionBucket` pushed every time a job completes
- Graph appends the data point in real-time (no HTTP fetch)
- Smooth animation: new point fades in, line animates to new position
- Two modes: "Live" (last 2 minutes, scrolling) and "Overview" (last 30 minutes)

#### 5.2 Multi-Metric Graph

Toggle between these metrics on the graph:
- Execution count per second (live)
- Average duration (ms)
- Error rate (%)
- Jobs executed (cumulative)

Each metric gets its own color and Y-axis scale.

#### 5.3 Job-Specific Graph

From Jobs page, click a job → "View Graph" button that opens the graph page filtered to that job's executions only.

**New endpoint**: `GET /api/stats?job=ReminderJob` — returns execution buckets filtered to one job.

### Phase 6: UI/UX Refinements

#### 6.1 Connection Status Indicator

In the footer bar:
- 🟢 Connected (SignalR active)
- 🟡 Reconnecting (shows retry count)
- 🔴 Disconnected (falls back to polling, shows "Live" vs "Polling" indicator)

#### 6.2 Toast Queue

Multiple toasts can stack. Each auto-dismisses after 3s but doesn't clear others. Max 3 visible.

#### 6.3 Command Palette

Press `Cmd+K` or `Ctrl+K`: fuzzy-search overlay to navigate pages, trigger jobs, filter data.

#### 6.4 Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `1`-`7` | Switch to page by position |
| `r` | Refresh current page |
| `/` | Focus search on Jobs/History |
| `Escape` | Close modals / clear search |
| `g` then `o` | Go to Overview |
| `g` then `j` | Go to Jobs |
| `g` then `t` | Go to Triggers |
| `g` then `g` | Go to Graph |

#### 6.5 Modal System

Replace inline expansions with a proper modal system for:
- Job creation
- Job edit (JobDataMap)
- Trigger creation
- Trigger edit
- Calendar add/edit
- Confirmation dialogs (delete actions)

Modals use Alpine.js `x-show` + `x-trap` (focus trap) + `x-transition`:
```html
<div x-show="showModal" x-trap.noscroll="showModal"
     class="fixed inset-0 z-50 flex items-center justify-center"
     x-transition:enter="transition ease-out duration-200"
     x-transition:enter-start="opacity-0"
     x-transition:enter-end="opacity-100">
  <!-- backdrop -->
  <div class="fixed inset-0 bg-black/60 backdrop-blur-sm" @click="showModal = false"></div>
  <!-- panel -->
  <div class="relative bg-gray-900 border border-gray-800 rounded-2xl shadow-2xl max-w-lg w-full mx-4 p-6"
       x-transition:enter="transition ease-out duration-200"
       x-transition:enter-start="opacity-0 scale-95"
       x-transition:enter-end="opacity-100 scale-100">
    ...
  </div>
</div>
```

#### 6.6 Better Responsive

- Sidebar auto-collapses on screens <1024px
- Tables scroll horizontally on mobile
- Stat cards stack 2×2 on tablet, 1×4 on desktop
- Modals go full-screen on mobile (<640px)

#### 6.7 Light Mode Toggle

Add light mode as an option in Settings. When enabled:
- Swap `bg-gray-900` → `bg-gray-50`, `text-gray-200` → `text-gray-800`
- Cards: `bg-white border-gray-200 shadow-sm`
- Sidebar: `bg-white border-r-gray-200`
- Keep quartz accent colors unchanged
- Uses `localStorage` for persistence + `prefers-color-scheme` media query default

---

## Files to Change / Create

| File | Action | Detail |
|------|--------|--------|
| `QuartzDashboard/wwwroot/index.html` | **Rewrite** | Add SignalR client, replace polling with events, add timeline, modals, keyboard shortcuts, command palette, light mode |
| `QuartzDashboard/SignalR/QuartzDashboardHub.cs` | **Create** | SignalR hub with Subscribe/Unsubscribe |
| `QuartzDashboard/Internal/DashboardEventBus.cs` | **Create** | In-memory event bus for decoupled pub/sub |
| `QuartzDashboard/Internal/DashboardSignalRBridge.cs` | **Create** | Bridges EventBus → SignalR hub |
| `QuartzDashboard/Internal/DashboardSchedulerListener.cs` | **Create** | ISchedulerListener for scheduler state changes |
| `QuartzDashboard/QuartzDashboardApplicationBuilderExtensions.cs` | **Modify** | Add MapHub for SignalR, add new API endpoints |
| `QuartzDashboard/QuartzDashboardServiceCollectionExtensions.cs` | **Modify** | Register SignalR, event bus, bridge, scheduler listener |
| `QuartzDashboard/QuartzDashboardOptions.cs` | **Modify** | Add `UseSignalR`, `HubPath` options |
| `QuartzDashboard/QuartzDashboard.csproj` | **Modify** | Add `Microsoft.AspNetCore.SignalR` package reference |
| `README.md` | **Update** | Document SignalR, real-time features, configuration UI |

---

## API Changes Summary

### New REST Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/jobs` | Create job |
| DELETE | `/api/jobs/{group}/{name}` | Delete job |
| PUT | `/api/jobs/{group}/{name}` | Update job (JobDataMap) |
| POST | `/api/triggers` | Create trigger |
| DELETE | `/api/triggers/{group}/{name}` | Delete trigger |
| POST | `/api/calendars` | Add calendar |
| GET | `/api/calendars` | List calendars |
| DELETE | `/api/calendars/{name}` | Delete calendar |
| GET | `/api/stats?job=Name` | Filtered stats |
| GET | `/api/timeline` | Last N fire events with precise timestamps |

### New SignalR Events

| Event | Payload | Trigger |
|-------|---------|---------|
| `jobExecuted` | `{ jobKey, triggerKey, duration, success, fireTime }` | Job completes |
| `jobTriggered` | `{ jobKey, triggerKey, fireTime }` | Job starts executing |
| `schedulerStatus` | `{ isStarted, isStandbyMode, isShutdown }` | Scheduler start/standby/shutdown |
| `jobsUpdated` | `{}` | Any job/trigger CRUD operation |
| `executionBucket` | `{ minute, count, avgDurationMs }` | Job completes (aggregated per minute) |

---

## Dependencies

### NuGet (new)
- `Microsoft.AspNetCore.SignalR` (included in ASP.NET Core — no additional NuGet needed, it's part of the framework reference)

### CDN (new for frontend)
- `@microsoft/signalr` 8.x (CDN: `https://cdn.jsdelivr.net/npm/@microsoft/signalr@8/dist/browser/signalr.min.js`)

---

## Implementation Order

1. **Backend: Event Bus + SignalR infrastructure**
   - Create `DashboardEventBus`
   - Create `QuartzDashboardHub`
   - Create `DashboardSignalRBridge`
   - Create `DashboardSchedulerListener`
   - Update `DashboardJobListener` to publish to event bus
   - Update service registration
   - Update middleware to map hub

2. **Backend: Configuration endpoints**
   - POST/DELETE jobs
   - POST/DELETE triggers
   - POST/DELETE calendars
   - GET timeline
   - Filtered stats by job

3. **Frontend: SignalR client integration**
   - Replace polling with event-driven updates
   - Add connection status indicator
   - Add fallback polling on disconnect
   - Optimistic UI updates for actions

4. **Frontend: Timeline page**
   - SVG timeline rendering
   - Real-time dot placement
   - Auto-scroll

5. **Frontend: Configuration modals**
   - Job creation modal
   - Trigger creation modal
   - Job edit modal (JobDataMap)
   - Calendar management
   - Delete confirmations

6. **Frontend: UI refinements**
   - Connection status in footer
   - Toast queue
   - Command palette (Cmd+K)
   - Keyboard shortcuts
   - Light mode toggle
   - Modal system

7. **Testing + Documentation**
   - Update demo app with SignalR
   - Verify real-time flow
   - Update README
   - Push to GitHub

---

## Risks & Tradeoffs

| Risk | Mitigation |
|------|-----------|
| **SignalR adds ~30KB to SPA** | Acceptable for a monitoring dashboard. The SPA is served once and cached. |
| **`AddSignalR()` may conflict with host app** | Use `services.TryAddSingleton<IConfigureOptions<SignalROptions>>(...)` pattern. Document that host apps need `app.MapHub` if already using SignalR. |
| **Job creation via API requires knowing .NET type names** | For the NuGet library, we can't know the host app's job types at runtime. Solution: provide an `AddQuartzJob<T>()` registration method, or let users specify type names as strings (the Quartz scheduler resolves them via `ITypeLoadHelper`). |
| **In-memory event bus doesn't scale across multiple nodes** | This is a single-node monitoring dashboard. Clustering support can be added later via Redis backplane. |
| **Light mode doubles CSS maintenance** | Use CSS custom properties (Tailwind doesn't easily support runtime theme switching). Alternative: ship two separate CSS files or use Tailwind's `dark:` prefix (current approach) and add light mode as a second class set. |
| **Full Quartz configuration requires deep Quartz API knowledge** | The UI abstracts this behind forms and modals. Complex cases (calendars with complex exclusion rules) can link to Quartz documentation. |

---

## Future Considerations

- **Persistent state**: Save execution buckets to a backing store (SQLite, EF Core) for dashboard data that survives restarts
- **Multi-node clustering**: Redis backplane for SignalR across multiple app instances
- **Alerts**: Configure thresholds (execution duration > X, error rate > Y%) and send notifications (webhook, email)
- **Export**: Download execution history as CSV/JSON
- **Embedded mode**: Embed individual chart widgets in other dashboards via iframe
