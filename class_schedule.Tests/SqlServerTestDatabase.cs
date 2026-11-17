using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using class_schedule.Infrastructure.Data;

namespace class_schedule.Tests;

// Only a successfully created database is owned; an uncertain CREATE is never cleaned up.
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string Prefix = "class_schedule_verify_";
    private readonly string name;
    private readonly string masterConnection;
    private bool owned;

    private SqlServerTestDatabase(string connection)
    {
        ConnectionString = BuildSqlConnectionString(connection);
        name = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        masterConnection = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString;
    }

    public string ConnectionString { get; }

    internal static string RequireConnection(string? enabled, string? connection)
    {
        if (enabled != "1") throw new InvalidOperationException("SQL Server verification is not enabled.");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("SQL Server verification connection is required.");
        try
        {
            var builder = new SqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(builder.DataSource) || !string.IsNullOrEmpty(builder.AttachDBFilename) || builder.UserInstance)
                throw new InvalidOperationException();
            return connection;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException("SQL Server verification connection is invalid or unsafe.");
        }
    }

    internal static bool IsSafeName(string name) => name.Length == Prefix.Length + 32 &&
        name.StartsWith(Prefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(name[Prefix.Length..], "N", out _) &&
        name[Prefix.Length..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void CheckName(string name)
    {
        if (!IsSafeName(name)) throw new InvalidOperationException("Unsafe verification database identifier.");
    }

    internal static string BuildSqlConnectionString(string connection)
    {
        var name = Prefix + Guid.NewGuid().ToString("N");
        CheckName(name);
        var builder = new SqlConnectionStringBuilder(RequireConnection("1", connection)) { InitialCatalog = name, Pooling = false };
        return builder.ConnectionString;
    }

    internal static async Task<SqlServerTestDatabase> CreateAsync(string connection)
    {
        var database = new SqlServerTestDatabase(connection);
        try
        {
            CheckName(database.name);
            await using var master = new SqlConnection(database.masterConnection);
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{database.name}]";
            await command.ExecuteNonQueryAsync();
            database.owned = true;
            var options = new DbContextOptionsBuilder<ClassScheduleDbContext>().UseSqlServer(database.ConnectionString).Options;
            await using var db = new ClassScheduleDbContext(options);
            await db.Database.MigrateAsync();
            return database;
        }
        catch
        {
            if (database.owned) await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!owned) return;
        CheckName(name);
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        CheckName(name);
        await using var command = master.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
        owned = false;
    }
}
