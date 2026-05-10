# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 3.0.x   | :white_check_mark: |
| 2.x     | :white_check_mark: |
| 1.x     | :x:                |

## Reporting a Vulnerability

**Do not open a public issue.** Instead, report security vulnerabilities privately:

- Use GitHub's private vulnerability reporting for this repository, if available.
- If private reporting is not available, contact the maintainer through the NuGet/GitHub profile listed in the package metadata.

You should receive a response within 48 hours. If the issue is confirmed, we will release a patch as soon as possible.

## Security Considerations

The dashboard is designed to be **opt-in** and **gateable**:

- Use `Enabled = false` in production if you don't want the dashboard accessible
- Use `RequireAuthentication`, `AllowedRoles`, or `RequiredPolicy` to restrict access
- The dashboard exposes scheduler internals — treat it like an admin panel
- Webhook URLs often contain credentials. The dashboard reports whether a webhook is configured, but does not expose the raw URL through `/api/config`.
- SQLite persistence uses the path you provide — ensure it's writable and secure
- SignalR hub negotiation is public by default — use auth middleware to protect it

## Dependencies

Dependencies are scanned automatically via Dependabot. Critical vulnerabilities in Quartz.NET or ASP.NET Core should be reported upstream:

- [Quartz.NET](https://github.com/quartznet/quartznet)
- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
