# SQL Server provider verification

The provider test is **not yet run** and is not production certification. Default tests skip it. Running it requires separate human authorization and a dedicated, disposable SQL Server 2022 or compatible instance; never use production or a shared server. The instance must permit CREATE DATABASE, ALTER DATABASE and DROP DATABASE. This document does not authorize an agent to connect or run it.

## Before an authorized run

1. Supply a private connection string without putting credentials in chat, shell history, source control or logs. Set `CLASS_SCHEDULE_SQLSERVER_TEST_CONNECTION` privately in the test process environment, and explicitly set `CLASS_SCHEDULE_SQLSERVER_TEST_ENABLED=1`. The connection must not use attached files or user instances.
2. Run `dotnet test ClassSchedule.sln --no-restore --filter FullyQualifiedName~SqlServerIntegrationTests` in that private environment. Inspect the result: the integration test must execute, not skip. An enabled test with a missing or invalid connection fails before database work.
3. Review the results and any cleanup failure. A failure is not provider proof.

The test overwrites the configured catalog with a fresh internal `class_schedule_verify_<GuidN>` name and explicitly creates the database on the supplied instance. It applies the real migration there, runs synthetic HTTP workflows, then closes its hosts before dropping only its owned database. The cleanup guard checks the generated name; ownership is recorded only after CREATE succeeds. If CREATE has an uncertain outcome, cleanup does not assume ownership; an operator must investigate the dedicated instance. Never repurpose this harness against an existing database.

## Safe default suite

Clear both variables even if inherited from a private environment:

```sh
env -u CLASS_SCHEDULE_SQLSERVER_TEST_ENABLED -u CLASS_SCHEDULE_SQLSERVER_TEST_CONNECTION dotnet test ClassSchedule.sln --no-restore
```

A skipped provider fact is expected in this suite and does not establish provider compatibility. The ordinary tests and pure safety checks use no SQL Server connection. No container, download or license acceptance is performed by these instructions.
