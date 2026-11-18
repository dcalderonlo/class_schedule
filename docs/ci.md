# Continuous integration

The [GitHub Actions workflow](../.github/workflows/ci.yml) validates every push and pull request. It can also be started manually from the Actions tab. It does not deploy, publish an application artifact or apply database migrations.

## Checks

1. Install the SDK selected by `global.json`, keeping CI aligned with the .NET 11 application and tests.
2. Restore dependencies with NuGet auditing enabled for direct and transitive packages. Restore warnings, including vulnerability advisories, fail the job.
3. Build in Release mode with compiler warnings treated as errors and build-time static analyzers enabled. Compilation checks C# syntax and types; this is not a blanket whitespace-formatting policy.
4. Run the test suite and produce a TRX report. Test failures fail the job.
5. Retain only the TRX reports as an Actions artifact for seven days, including when the test step fails and produces a report.

The workflow uses a GitHub-hosted Ubuntu runner, read-only repository permissions and commit-pinned official actions. Checkout does not retain Git credentials. Superseded runs for the same workflow/ref are cancelled, and each job is limited to 15 minutes.

## Database and secret boundaries

CI needs no deployment credentials, Vercel IDs or private environment files. It does not read `.env.vercel.local`, export repository secrets or upload the project directory.

The ordinary tests use isolated relational test state. The real SQL Server integration test is deliberately skipped: the workflow disables and explicitly unsets both opt-in variables before running tests. That skip is expected, not proof of provider verification.

No local Docker instance is contacted. Adding a separately authorized SQL Server CI service or a CD workflow is future work.

## Local equivalent

```sh
dotnet restore ClassSchedule.sln -p:NuGetAudit=true -p:NuGetAuditMode=all -warnaserror
dotnet build ClassSchedule.sln --configuration Release --no-restore -warnaserror -p:RunAnalyzersDuringBuild=true
env -u CLASS_SCHEDULE_SQLSERVER_TEST_ENABLED -u CLASS_SCHEDULE_SQLSERVER_TEST_CONNECTION \
  dotnet test ClassSchedule.sln --configuration Release --no-build --no-restore
```

Local command results and YAML inspection do not establish that a hosted Actions run passed. The first GitHub run can be checked after the workflow is committed and pushed with user authorization.
