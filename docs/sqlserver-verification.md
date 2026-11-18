# SQL Server provider verification

The provider test **passed on November 18, 2026** against the explicitly authorized local SQL Server 2022 Docker container. This is not production certification. Default tests still skip it. Future runs require separate human authorization and a dedicated, disposable SQL Server 2022 or compatible instance; never use production or a shared server. The instance must permit CREATE DATABASE, ALTER DATABASE and DROP DATABASE. This document does not authorize an agent to connect or run it.

## Observed verification

- The existing `Real_provider_migration_bootstrap_and_enrollment_workflow` test executed: 1 passed, 0 failed, 0 skipped; exit code 0. It covers the real migration, development administrator bootstrap, concurrent capacity and personal-conflict enforcement, enrollment privacy, notifications and rollback after an injected notification failure.
- The container had no published host port. A temporary loopback-only tunnel at `127.0.0.1:14330` provided access without restarting or recreating it. The tunnel was closed afterward; no listener remained.
- Post-run inspection found no `class_schedule_verify_` databases. The six original database IDs, including `tempdb`, remained online. No migration was applied to an existing database.
- Before testing, five database backups excluding `tempdb` passed `RESTORE VERIFYONLY WITH CHECKSUM`; host copies matched their container SHA-256 hashes. This was backup verification, not an actual restore rehearsal.
- The ordinary suite separately passed 54 tests with the one opt-in provider test skipped. These results do not establish production readiness or provide persistent host connectivity.

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
