# Contributing to Dot.QuartzDashboard

Thanks for your interest in contributing! Here's how to get started.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold this code.

## Getting Started

### Prerequisites

- [.NET 8.0+ SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org) (for frontend asset bundling)

### Setup

```bash
git clone https://github.com/nathan5580/QuartzDashboard.git
cd QuartzDashboard
dotnet restore
cd QuartzDashboard && npm ci
```

### Build & Test

```bash
# Build
dotnet build -c Release

# Run unit tests (95 tests)
dotnet test QuartzDashboard.Tests -c Release

# Run integration tests (61 tests)
dotnet test QuartzDashboard.IntegrationTests -c Release

# Run demo app
dotnet run --project QuartzDashboard.Demo
```

Open `http://localhost:5190/quartz` to see the dashboard in action.

## How to Contribute

### Reporting Bugs

Open an issue using the **Bug Report** template. Include:
- .NET version and OS
- Steps to reproduce
- Expected vs actual behavior
- Screenshots if applicable

### Suggesting Features

Open an issue using the **Feature Request** template. Describe:
- The problem you're solving
- Your proposed solution
- Alternatives you've considered

### Pull Requests

1. Fork the repo and create a branch from `main`
2. Make your changes, following the conventions below
3. Add/update tests
4. Ensure all tests pass: `dotnet test -c Release`
5. Open a PR with a clear description

## Code Conventions

- **Backend**: Follow ASP.NET Core conventions. New API handlers go in `Handlers/`, models in `Models/`.
- **Frontend**: The SPA is a single `index.html` Alpine.js component. Use `this.api(endpoint)` for all fetch URLs. Loading states required on all action buttons.
- **Tests**: Use xUnit. New features need both unit and integration tests.
- **Commits**: Follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `test:`, `ci:`, `chore:`).

## Architecture

```
QuartzDashboard/
├── QuartzDashboard/            # NuGet library
│   ├── Handlers/               # API handlers by feature
│   ├── Models/                 # Request/response DTOs
│   ├── Services/               # Business logic
│   ├── Middleware/             # Auth middleware
│   ├── SignalR/                # Real-time hub
│   └── wwwroot/                # Embedded SPA (Alpine.js)
├── QuartzDashboard.Tests/      # Unit tests
├── QuartzDashboard.IntegrationTests/  # Integration tests
└── QuartzDashboard.Demo/       # Demo app
```

## Questions?

Open a [discussion](https://github.com/nathan5580/QuartzDashboard/discussions) or start an issue.
