# AI Prompt — Dot.QuartzDashboard

> Copy this into any Copilot / AI assistant for instant, complete knowledge of the package.

```
You are integrating Dot.QuartzDashboard (NuGet) into an ASP.NET Core app.

PACKAGES (v4 — split into three):
  Dot.QuartzDashboard               — middleware + handlers + SPA + in-memory/JSON history
  Dot.QuartzDashboard.Abstractions  — IFireHistoryStore + FireRecord (no ASP.NET deps)
  Dot.QuartzDashboard.Sqlite        — SqliteFireHistoryStore + AddQuartzDashboardSqliteHistory()
CURRENT VERSION: 4.2.2
TARGETS: net8.0, net9.0, net10.0
NAMESPACES:
  QuartzDashboard                  — middleware, options, hub
  QuartzDashboard.Abstractions     — interfaces + records
  QuartzDashboard.Sqlite           — SQLite store + DI extension
  QuartzDashboard.Models           — request + response DTOs
DASHBOARD URL: /quartz (or options.Path)

--- MINIMUM SETUP (2 lines) ---

// 1. In DI (Program.cs or ServiceExtensions):
builder.Services.AddQuartzDashboard();

// 2. In middleware pipeline (before MapControllers and MapFallbackToFile):
app.UseQuartzDashboard();

That's it. Open /quartz in the browser.

--- FULL OPTIONS ---

builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/quartz";               // dashboard route prefix (default: "/quartz")
    options.Enabled = true;                  // false = UseQuartzDashboard() is a no-op
    options.ReadOnly = false;                // disable trigger/start/stop/delete actions
    options.UseSignalR = true;               // real-time push updates via SignalR

    // Auth (checked in order: auth → policy → roles)
    options.RequireAuthentication = false;   // require authenticated user (401 if not)
    options.RequiredPolicy = "";             // named IAuthorizationService policy (403 if fails)
    options.AllowedRoles = [];               // role whitelist — checked if no policy set (403 if fails)

    // History limits
    options.MaxFireHistory = 500;            // max fire records in memory (default: 500)
    options.MaxExecutionLogsPerJob = 50;     // max log lines per job
    options.HistoryRetentionHours = 24;      // auto-prune records older than this (0 = keep all)
    options.Title = "My App Dashboard";      // custom title in sidebar + browser tab

    // Persistence (survive restarts)
    // NOTE: For SQLite persistence, do NOT set options.PersistHistoryToSqlite (removed in v4).
    // Instead, reference Dot.QuartzDashboard.Sqlite and call:
    //   builder.Services.AddQuartzDashboardSqliteHistory("quartz-history.db");
    options.PersistHistoryPath = "quartz-history.json";   // optional JSON fallback when SQLite is not used

    // Callbacks
    options.OnJobFailed = async (jobKey, ex) => { /* Slack/PagerDuty alert */ };
    options.WebhookUrl = "https://hooks.slack.com/...";  // POST JSON on job failure
});

--- APPSETTINGS BINDING (bind a config section directly) ---

// In appsettings.json:
{
  "QuartzDashboard": {
    "Enabled": true,
    "Path": "/quartz",
    "ReadOnly": false,
    "UseSignalR": true,
    "RequireAuthentication": false,
    "RequiredPolicy": "",
    "AllowedRoles": [],
    "MaxFireHistory": 500,
    "MaxExecutionLogsPerJob": 50
  }
}

// In code:
builder.Services.AddQuartzDashboard(options =>
    builder.Configuration.GetSection("QuartzDashboard").Bind(options));

--- ENVIRONMENT GATING ---

builder.Services.AddQuartzDashboard(options =>
{
    options.Enabled = !builder.Environment.IsProduction();
});

--- MIDDLEWARE ORDER RULES ---
- UseQuartzDashboard() must come BEFORE app.MapControllers() and app.MapFallbackToFile()
- If using auth, app.UseAuthentication() and app.UseAuthorization() must come BEFORE UseQuartzDashboard()
- UseSignalR = true makes the NuGet register its own SignalR hub — do NOT manually call app.MapHub<QuartzDashboardHub>()
- The NuGet handles /quartz → /quartz/ redirect automatically

--- API ENDPOINTS (all under {Path}/api/) ---
GET    /scheduler                        - scheduler metadata, status, uptime
POST   /scheduler/start                  - start or resume from standby
POST   /scheduler/standby                - put scheduler in standby
GET    /schedulers                       - all registered schedulers
GET    /jobs                             - all jobs with triggers (?offset=0&limit=50)
POST   /jobs                             - create a job
GET    /jobs/{group}/{name}              - single job detail with JobDataMap
PUT    /jobs/{group}/{name}              - update job description/data map
DELETE /jobs/{group}/{name}              - delete job
GET    /jobs/{group}/{name}/logs         - recent execution log lines
POST   /jobs/{group}/{name}/trigger      - fire immediately
POST   /jobs/{group}/{name}/pause        - pause job
POST   /jobs/{group}/{name}/resume       - resume job
POST   /jobs/{group}/{name}/interrupt    - interrupt executing job
POST   /jobs/group/{group}/pause         - pause all jobs in group
POST   /jobs/group/{group}/resume        - resume all jobs in group
POST   /jobs/batch/pause                 - pause a set of jobs
POST   /jobs/batch/resume                - resume a set of jobs
POST   /jobs/batch/trigger               - fire a set of jobs
POST   /jobs/batch/delete                - delete a set of jobs
GET    /triggers                         - all triggers (?offset=0&limit=50)
POST   /triggers                         - create a trigger (cron or simple)
GET    /triggers/{group}/{name}          - single trigger detail
PUT    /triggers/{group}/{name}          - update trigger schedule
DELETE /triggers/{group}/{name}          - unschedule trigger
GET    /triggers/{group}/{name}/next-fires - next N fire times (?count=10)
POST   /triggers/{group}/{name}/pause    - pause trigger
POST   /triggers/{group}/{name}/resume   - resume trigger
POST   /triggers/group/{group}/pause     - pause all triggers in group
POST   /triggers/group/{group}/resume    - resume all triggers in group
GET    /calendars                        - all calendars
POST   /calendars                        - create a calendar
DELETE /calendars/{name}                 - delete a calendar
GET    /executing                        - currently running jobs with duration
GET    /history                          - paginated fire events (?offset=0&limit=50&job=group.name)
GET    /stats                            - per-minute buckets + rates + P50/P95/P99
GET    /stats/history                    - rolling history for the graph
GET    /health                           - success rate, thread pool, failure list
GET    /timeline                         - execution timeline (up to 500 records)
GET    /heatmap                          - execution density grid (day × hour)
GET    /config                           - dashboard config snapshot
POST   /cron/describe                    - validate CRON + return next 5 fire times
GET    /export                           - export all jobs+triggers as JSON
POST   /import                           - import jobs+triggers from export payload

--- SIGNALR HUB ---
Hub class: QuartzDashboard.QuartzDashboardHub
Default endpoint: {Path}/hub  (e.g. /quartz/hub)
Registered automatically when UseSignalR = true — no manual MapHub needed.
POST {Path}/hub/negotiate?negotiateVersion=1  → 200 when working

--- UI FEATURES (v4.2.x) ---
- Anti-flicker refresh — mergeArrayInPlace keeps DOM nodes stable across auto-refresh cycles
- Silent background refresh — SignalR/auto-refresh updates skip loading spinners and error toasts
- Row density toggle (comfortable / compact), persisted to localStorage
- Desktop notifications for job failures (opt-in browser permission)
- Per-job sparkline column on Jobs page (xl ≥ 1280px)
- "In-memory only" banner on History when no persistent store is registered
- Triggers group header with Pause/Resume context buttons and paused-count badge
- Favicon failure badge — red dot on browser tab when unacknowledged failures exist
- CSV export and JSON export from History page
- Print report from History/Health pages
- Graph page: dual-line SVG (execution count + avg duration + error rate), zoom toggles
- Timeline page: full-width Gantt bars, crosshair tooltip, pulsing now-marker
- Mobile bottom tab bar (all 10 pages, horizontally scrollable)
- Command palette (⌘K): "Run now: X.Y" label, keyword aliases (run/fire/trigger/execute)
- Clickable stat cards on Overview — navigate to Jobs / Triggers / Executing / History
- History date-range filters: 1h / 6h / 24h / All with inline error snippets on failed rows

--- COMMON MISTAKES ---
- Do NOT call app.MapHub<QuartzDashboardHub>() yourself when UseSignalR = true
- Do NOT place UseQuartzDashboard() after MapFallbackToFile — Blazor WASM will swallow all /quartz routes
- Visiting /quartz (no trailing slash) works — NuGet redirects to /quartz/ automatically
- Dashboard assets are embedded in the package — no external CDN allowlist is required
```
