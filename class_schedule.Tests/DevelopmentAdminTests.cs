using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using class_schedule.Application.Security;
using Xunit;

namespace class_schedule.Tests;

public class DevelopmentAdminTests
{
    private const string Email = "admin@example.test";
    private const string Password = "StrongPassword1!";
    private const string OtherPassword = "AnotherStrong1!";
    private static Dictionary<string, string?> Config(string? email = Email, string? password = Password) => new()
    {
        ["DevelopmentAdmin:Enabled"] = "true", ["DevelopmentAdmin:Email"] = email, ["DevelopmentAdmin:Password"] = password
    };
    private static SqliteConnection Database() { var db = new SqliteConnection("Data Source=:memory:"); db.Open(); return db; }
    private static HttpClient Client(DevelopmentAdminApplicationFactory app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var data = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", data.GetProperty("requestToken").GetString());
    }
    private static Task<HttpResponseMessage> Credentials(HttpClient client, string action, string email, string password) =>
        client.PostAsJsonAsync("/api/account/" + action, new { email, password, role = "Administrator" });
    private static Task<HttpResponseMessage> AdminRoute(HttpClient client) =>
        client.PostAsJsonAsync("/api/catalog/subjects", new { name = "Math", code = "MAT", weeklyHours = 4 });

    private static async Task<string> SingleAdminId(HttpClient client)
    {
        var response = await client.GetAsync("/api/enrollments/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        var user = Assert.Single(users.EnumerateArray());
        Assert.Equal(Email, user.GetProperty("email").GetString());
        return Assert.IsType<string>(user.GetProperty("id").GetString());
    }

    private sealed class FailingUserValidator(int failAt) : IUserValidator<IdentityUser>
    {
        public int Calls { get; private set; }
        public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
        {
            Calls++;
            return Task.FromResult(Calls == failAt
                ? IdentityResult.Failed(new IdentityError { Description = "injected private detail" })
                : IdentityResult.Success);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Identity_failure_rolls_back_bootstrap_and_allows_ordinary_registration(int failAt)
    {
        using var db = Database();
        var validator = new FailingUserValidator(failAt);
        await using (var failed = new DevelopmentAdminApplicationFactory(db, "Development", Config(),
            services => services.AddSingleton<IUserValidator<IdentityUser>>(validator)))
        {
            var exception = Assert.ThrowsAny<Exception>(() => Client(failed));
            Assert.Equal(failAt, validator.Calls);
            Assert.Contains("Development administrator bootstrap failed.", exception.ToString());
            Assert.DoesNotContain(Email, exception.ToString());
            Assert.DoesNotContain(Password, exception.ToString());
            Assert.DoesNotContain("injected private detail", exception.ToString());
        }
        await using var healthy = new DevelopmentAdminApplicationFactory(db, "Development", new());
        using var client = Client(healthy);
        await Token(client);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register", Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login", Email, Password)).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminRoute(client)).StatusCode);
    }

    [Fact]
    public async Task Enabled_development_creates_admin_but_public_registration_does_not()
    {
        using var db = Database();
        await using var app = new DevelopmentAdminApplicationFactory(db, "Development", Config());
        using var admin = Client(app);
        await Token(admin);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(admin, "login", Email, Password)).StatusCode);
        await Token(admin);
        Assert.Equal(HttpStatusCode.Created, (await AdminRoute(admin)).StatusCode);
        using var student = Client(app);
        await Token(student);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(student, "register", "student@example.test", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(student, "login", "student@example.test", Password)).StatusCode);
        await Token(student);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminRoute(student)).StatusCode);
    }

    [Theory]
    [InlineData("Production", Email, Password)]
    [InlineData("Development", null, Password)]
    [InlineData("Development", "invalid", Password)]
    [InlineData("Development", Email, null)]
    [InlineData("Development", Email, "weak")]
    public async Task Unsafe_configuration_refuses_start_and_leaves_database_usable(string environment, string? email, string? password)
    {
        using var db = Database();
        await using (var failed = new DevelopmentAdminApplicationFactory(db, environment, Config(email, password)))
        {
            var exception = Assert.ThrowsAny<Exception>(() => Client(failed));
            Assert.DoesNotContain(Password, exception.ToString());
            Assert.DoesNotContain(Email, exception.ToString());
        }
        await using var healthy = new DevelopmentAdminApplicationFactory(db, "Development", new());
        using var client = Client(healthy);
        await Token(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(client, "login", Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register", Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login", Email, Password)).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminRoute(client)).StatusCode);
    }

    [Fact]
    public async Task Existing_user_is_not_promoted_or_reset()
    {
        using var db = Database();
        await using (var initial = new DevelopmentAdminApplicationFactory(db, "Development", new()))
        using (var client = Client(initial))
        {
            await Token(client);
            Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register", Email, Password)).StatusCode);
        }
        await using (var failed = new DevelopmentAdminApplicationFactory(db, "Development", Config(Email, OtherPassword)))
            Assert.ThrowsAny<Exception>(() => Client(failed));
        await using var healthy = new DevelopmentAdminApplicationFactory(db, "Development", new());
        using var again = Client(healthy);
        await Token(again);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(again, "login", Email, OtherPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(again, "login", Email, Password)).StatusCode);
        await Token(again);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminRoute(again)).StatusCode);
    }

    [Fact]
    public async Task Another_existing_user_blocks_creation_and_remains_ordinary()
    {
        using var db = Database();
        await using (var initial = new DevelopmentAdminApplicationFactory(db, "Development", new()))
        using (var client = Client(initial))
        {
            await Token(client);
            Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register", "other@example.test", Password)).StatusCode);
        }
        await using (var failed = new DevelopmentAdminApplicationFactory(db, "Development", Config()))
            Assert.ThrowsAny<Exception>(() => Client(failed));
        await using var healthy = new DevelopmentAdminApplicationFactory(db, "Development", new());
        using var client2 = Client(healthy);
        await Token(client2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(client2, "login", Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client2, "login", "other@example.test", Password)).StatusCode);
        await Token(client2);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminRoute(client2)).StatusCode);
    }

    [Fact]
    public async Task Existing_empty_administrator_role_is_reused()
    {
        using var db = Database();
        await using (var initial = new DevelopmentAdminApplicationFactory(db, "Development", new()))
        {
            using var client = Client(initial);
            using var scope = initial.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded);
        }
        await using var app = new DevelopmentAdminApplicationFactory(db, "Development", Config());
        using var admin = Client(app);
        await Token(admin);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(admin, "login", Email, Password)).StatusCode);
        await Token(admin);
        Assert.Equal(HttpStatusCode.Created, (await AdminRoute(admin)).StatusCode);
    }

    [Fact]
    public async Task Existing_admin_is_idempotent_and_password_is_not_reset()
    {
        using var db = Database();
        string originalId;
        await using (var first = new DevelopmentAdminApplicationFactory(db, "Development", Config()))
        using (var client = Client(first))
        {
            await Token(client);
            Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login", Email, Password)).StatusCode);
            originalId = await SingleAdminId(client);
        }
        await using var second = new DevelopmentAdminApplicationFactory(db, "Development", Config(Email, OtherPassword));
        using var again = Client(second);
        await Token(again);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(again, "login", Email, OtherPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(again, "login", Email, Password)).StatusCode);
        Assert.Equal(originalId, await SingleAdminId(again));
        await Token(again);
        Assert.Equal(HttpStatusCode.Created, (await AdminRoute(again)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(again, "register", Email, OtherPassword)).StatusCode);
    }
}
