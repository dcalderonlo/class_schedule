# SQL Server schema setup

The initial EF migration is for a **new, empty database only**. It does not convert the unmanaged prototype's `SubjectId`/`GradeLevel` tables or data. For an existing database, take a verified backup and obtain separate authorization for an explicit mapping and conversion plan; do not apply this initial migration to it.

## Quick path

From the repository root with the .NET 11 SDK, restore, build and test as described in the [README](../README.md). Then, with network access to the configured NuGet sources, an operator can restore the pinned EF Core 11 tool:

```sh
dotnet tool restore
```

Supply `ConnectionStrings__ClassSchedule` using your platform's secret manager or a private environment variable. Use a SQL Server connection string for the intended empty target; do not put credentials in source, shell history, or logs. Both application configuration and the design-time factory use this setting. The design-time factory fails if it is missing; the application currently does not fail at startup solely because it is missing.

Review `class_schedule/Infrastructure/Data/Migrations/` and generate SQL for inspection without applying it:

```sh
dotnet ef migrations script --project class_schedule --startup-project class_schedule --output initial-review.sql
```

Keep the review output outside version control and protect it according to local policy. Confirm the target is empty, the connection and permissions are correct, and the generated SQL is approved. **Only if explicitly authorized** should an operator apply the migration:

```sh
dotnet ef database update --project class_schedule --startup-project class_schedule
```

Nothing in application startup applies migrations. The migration's `Down` drops tables and data: any rollback requires a verified backup and explicit operator authorization. Do not use a blanket database reset or restore as a setup step.

## Boundaries

The migration and its offline-generated SQL represent the current Identity, catalog, scheduling, enrollment and notification model. Offline metadata and SQL generation do not prove SQL Server execution, concurrency behavior, or a successful migration on a real server. This schema does not create an administrator or seed credentials. After the reviewed schema is applied, see the default-disabled [development initial administrator procedure](initial-admin.md); production provisioning remains separate.
