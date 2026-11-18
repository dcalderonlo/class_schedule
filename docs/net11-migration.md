# .NET 11 migration

The application and tests target `net11.0`, using stable SDK 11.0.100 and the corresponding ASP.NET Core, Identity and EF Core 11 packages. Execution evidence and remaining checks are tracked in [the migration task document](../odd/tasks/net11-migration.md); changing package versions alone is not verification.

## What changes and why

| Area | Change | Expected benefit |
| --- | --- | --- |
| SDK selection | `global.json` selects SDK 11; application and test projects target `net11.0`. | Normal `dotnet` commands use the intended toolchain rather than requiring the ignored local SDK 10 installation. |
| Web platform | ASP.NET Core and its HTTP test host move together to 11. | Adopt the supported web platform and its runtime behavior while testing the same public endpoints. |
| Persistence | EF Core, SQL Server/SQLite providers and the pinned EF tool move together to 11. | Avoid mixed-major framework/provider tooling and use the new query engine without speculative query rewrites. |
| Runtime and language | Target .NET 11 with its default C# 15 language version. | Receive applicable runtime/JIT improvements and language support without forcing new syntax into working business rules. No application-specific speedup is claimed without measurements. |
| Cryptography | Remove the redundant explicit PKCS package reference, as diagnosed by SDK warning NU1510. | ASP.NET Core 11 already supplies the assembly; avoid a duplicate version pin while retaining the dependency advisory audit. |

## Security and design boundaries

- Keep `UseAuthentication()`, `UseAuthorization()` and token-based `UseAntiforgery()` in their existing order. ASP.NET Core 11 introduces automatic header-based CSRF protection, but removing token middleware would change Blazor form token generation and validation. The framework upgrade is not permission to weaken or replace the existing cookie/token contract.
- Preserve Identity-managed credentials, secure cookies, server-derived actors and administrator policies. Do not replace Identity with custom JWT/password handling.
- Preserve the existing application-service interfaces and dependency-injection registrations. Framework migration does not require a new repository, mediator or generic service layer.
- Keep conflict checks, serializable transactions and transactional notifications together. Query optimization must not separate checks from the protected write or change the public error contract.
- Keep the initial migration and its generated historical metadata. EF model compatibility must be checked under EF 11; a runtime upgrade does not justify recreating existing databases or regenerating migration history.

These choices apply SOLID by retaining clear responsibilities and existing abstractions, rather than adding patterns solely to demonstrate a framework release.

## Verification and cleanup

Build and run the default suite with the commands in the [README](../README.md). Restore the pinned local EF tool with `dotnet tool restore`. Schema tests inspect the SQL Server model without opening a database connection.

The real SQL Server workflow remains a separate, explicitly authorized opt-in check against a fresh harness-owned database. A previous .NET 10 integration pass is historical evidence, not proof of .NET 11 compatibility.

After removal of the redundant PKCS reference, .NET 11 restore and build passed with zero warnings/errors, followed by 54 ordinary tests and two schema tests passing. The real-provider test was skipped in that default run. The EF tool restored and reported version 11.0.0. The configured NuGet advisory sources reported no vulnerable packages at the time of the post-cleanup check.

The separate .NET 11 SQL Server run passed one test with zero failures or skips. Postchecks found the same six original databases, no verification database and no listener on the temporary port. The existing container remained running with no published host ports.

Independent isolated verification passed SDK selection, both target frameworks, C# 15, an offline build and a runtime/assembly-version probe, with no blockers. That probe is now retained as `PlatformVersionTests`; the final default suite passes 55 tests with the opt-in SQL test skipped. The separately passing real-provider test is not counted as skipped integration proof.

Active SDK, target-framework, framework-package and operator requirements move to 11. Historical task logs and migration metadata are retained. `MinimumVisualStudioVersion = 10.0.40219.1` in the solution describes the solution format's Visual Studio compatibility, not a .NET 10 runtime dependency.

Ignored SDK installations, package caches, build output and database backups are not removed by the migration. No global SDK uninstall, container recreation, existing-database migration or publishing is implied.

## References

- [.NET 11 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json)
- [.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
- [ASP.NET Core 10 to 11 migration](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110)
- [Blazor SSR antiforgery behavior change](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/blazor-server-side-rendering-deferred-cross-site-request-forgery-protection)
- [EF Core 11 changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/whatsnew)
