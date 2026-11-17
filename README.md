# Class Schedule

A university scheduling and enrollment portfolio application built with ASP.NET Core 10, Blazor and EF Core SQL Server. The university model and secure self-service enrollment are being implemented in reviewable units; see [the feature specification](odd/tasks/secure-university-enrollment.md) for current status.

## Build and test

Requires the .NET 10 SDK and ASP.NET Core runtime. Do not retarget the application merely because a newer SDK is installed.

```sh
dotnet restore ClassSchedule.sln
dotnet build ClassSchedule.sln --no-restore
dotnet test ClassSchedule.sln --no-build
```

If a project-local SDK is installed under `.tools/dotnet`, replace `dotnet` with `.tools/dotnet/dotnet`. Generated build output and local tools are ignored, not deleted.

## Implementation boundaries

- Keep a modular monolith: HTTP/UI, application use cases, domain rules and EF persistence.
- Use ASP.NET Core Identity with secure cookies for same-origin authentication; cookies do not require JWT.
- An enrollment selects a section for one subject offering in an academic period. A section can contain multiple meetings.
- Different sections may meet simultaneously when their rooms, teachers and participants do not conflict.
- No overnight meetings. Room capacity and subject weekly-hour limits must be enforced.
- Database configuration and migrations are not applied automatically; never migrate an existing database without a backup and explicit approval.

See [authentication setup and API usage](docs/authentication.md). Academic management and enrollment endpoints remain future work. No secrets or default administrator credentials belong in source control.
