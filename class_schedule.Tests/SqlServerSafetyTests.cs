using Microsoft.Data.SqlClient;
using Xunit;

namespace class_schedule.Tests;

public class SqlServerSafetyTests
{
    [Fact]
    public void Configured_catalog_is_replaced_with_fresh_safe_names()
    {
        const string connection = "Server=localhost;Initial Catalog=existing;Integrated Security=true";
        var first = new SqlConnectionStringBuilder(SqlServerTestDatabase.BuildSqlConnectionString(connection));
        var second = new SqlConnectionStringBuilder(SqlServerTestDatabase.BuildSqlConnectionString(connection));
        Assert.True(SqlServerTestDatabase.IsSafeName(first.InitialCatalog));
        Assert.True(SqlServerTestDatabase.IsSafeName(second.InitialCatalog));
        Assert.NotEqual(first.InitialCatalog, second.InitialCatalog);
        Assert.NotEqual("existing", first.InitialCatalog);
        Assert.False(first.Pooling);
    }

    [Theory]
    [InlineData("master")]
    [InlineData("class_schedule_verify_00000000000000000000000000000000]; DROP DATABASE master--")]
    [InlineData("class_schedule_verify_0000000000000000000000000000000G")]
    public void Reject_foreign_or_malicious_names(string name) => Assert.False(SqlServerTestDatabase.IsSafeName(name));

    [Theory]
    [InlineData("Server=localhost;AttachDBFilename=/tmp/other.mdf;Integrated Security=true")]
    [InlineData("Server=localhost;User Instance=true;Integrated Security=true")]
    [InlineData("Context Connection=true")]
    [InlineData("not a connection string")]
    public void Reject_unsafe_connection(string connection) =>
        Assert.Throws<InvalidOperationException>(() => SqlServerTestDatabase.BuildSqlConnectionString(connection));

    [Fact]
    public void Enabled_without_private_connection_fails() =>
        Assert.Throws<InvalidOperationException>(() => SqlServerTestDatabase.RequireConnection("1", null));
}
