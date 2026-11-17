using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using class_schedule.Application.Security;
using class_schedule.Domain.Models.Entities;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace class_schedule.Tests;

public class AuthenticationTests
{
    private const string Email = "student@example.test";
    private const string Password = "StrongPassword1!";
    private static HttpClient Client(AuthenticationApplicationFactory app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task Token(HttpClient client)
    {
        var response = await client.GetAsync("/api/account/csrf");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
    }

    private static Task<HttpResponseMessage> Credentials(HttpClient c, string route, object? data = null) =>
        c.PostAsJsonAsync("/api/account/" + route, data ?? new { email = Email, password = Password });

    private static void AssertCookie(HttpResponseMessage response, string name, bool session)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(name + "=", StringComparison.Ordinal));
        var parts = cookie.Split(';', StringSplitOptions.TrimEntries);
        Assert.Contains(parts, p => p.Equals("path=/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, p => p.Equals("secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, p => p.Equals("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, p => p.Equals("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parts, p => p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));
        if (session)
        {
            Assert.DoesNotContain(parts, p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(parts, p => p.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task AssertApiFailure(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<html", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task MakeAdministrator(AuthenticationApplicationFactory app)
    {
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded);
        var user = await users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.True((await users.AddToRoleAsync(user, SecurityPolicies.Administrator)).Succeeded);
    }

    [Fact]
    public async Task Csrf_endpoint_returns_token_and_secure_cookie()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        var response = await client.GetAsync("/api/account/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrEmpty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString()));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        AssertCookie(response, "__Host-class-schedule-csrf", false);
    }

    [Fact]
    public async Task Register_login_me_logout_and_secure_session_cookie()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Token(client);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register")).StatusCode);
        var login = await Credentials(client, "login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertCookie(login, "__Host-class-schedule", true);
        var me = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(Email, (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString());
        await Token(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/account/logout", null)).StatusCode);
        await AssertApiFailure(await client.GetAsync("/api/account/me"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Csrf_rejection_does_not_create_account_or_sign_in_or_out()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(client, "register")).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(client, "login")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(client, "login")).StatusCode);
        await AssertApiFailure(await client.GetAsync("/api/account/me"), HttpStatusCode.Unauthorized);
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/account/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_student_cannot_mutate_schedule_or_escalate_role()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await AssertApiFailure(await client.GetAsync("/api/schedule"), HttpStatusCode.Unauthorized);
        await Token(client);
        await AssertApiFailure(await client.PostAsJsonAsync("/api/schedule", new { }), HttpStatusCode.Unauthorized);
        await AssertApiFailure(await client.PutAsJsonAsync("/api/schedule/1", new { }), HttpStatusCode.Unauthorized);
        await AssertApiFailure(await client.DeleteAsync("/api/schedule/1"), HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register", new { email = Email, password = Password, role = "Administrator", id = "chosen" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login")).StatusCode);
        var before = await client.GetStringAsync("/api/schedule");
        await Token(client);
        await AssertApiFailure(await client.PostAsJsonAsync("/api/schedule", new { }), HttpStatusCode.Forbidden);
        await AssertApiFailure(await client.PutAsJsonAsync("/api/schedule/1", new { }), HttpStatusCode.Forbidden);
        await AssertApiFailure(await client.DeleteAsync("/api/schedule/1"), HttpStatusCode.Forbidden);
        Assert.Equal(before, await client.GetStringAsync("/api/schedule"));
    }

    [Fact]
    public async Task Administrator_preserves_schedule_crud_conflicts_and_csrf()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Token(client);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register")).StatusCode);
        await MakeAdministrator(app);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassScheduleDbContext>();
            db.Subjects.Add(new Subject { Id = 1, Name = "Mathematics", Code = "MAT-1", WeeklyHours = 4 });
            db.Teachers.Add(new Teacher { Id = 1, FirstName = "Ada", LastName = "Lovelace", Email = "teacher@example.test" });
            db.Rooms.Add(new Room { Id = 1, Number = "A-101", Capacity = 30 });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login")).StatusCode);
        await Token(client);
        var create = new { subjectId = 1, teacherId = 1, roomId = 1, dayOfWeek = 1, startTime = "09:00:00", endTime = "10:00:00", gradeLevel = 1 };
        var created = await client.PostAsJsonAsync("/api/schedule", create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var item = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = item.GetProperty("id").GetInt32();
        Assert.EndsWith("/api/Schedule/" + id, created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Mathematics", item.GetProperty("subjectName").GetString());
        Assert.Equal("Ada Lovelace", item.GetProperty("teacherFullName").GetString());
        Assert.Equal("A-101", item.GetProperty("roomNumber").GetString());
        var get = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(item.ToString(), (await get.Content.ReadFromJsonAsync<JsonElement>()).ToString());
        var list = await client.GetAsync("/api/schedule");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(items.EnumerateArray());
        Assert.Equal(item.ToString(), items[0].ToString());

        var before = await client.GetStringAsync("/api/schedule");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/schedule/{id}")).StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/schedule"));
        await Token(client);
        var conflict = await client.PostAsJsonAsync("/api/schedule", create);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()));
        Assert.Equal(before, await client.GetStringAsync("/api/schedule"));

        var update = new { subjectId = 1, teacherId = 1, roomId = 1, dayOfWeek = 2, startTime = "11:00:00", endTime = "12:00:00", gradeLevel = 2 };
        var updated = await client.PutAsJsonAsync($"/api/schedule/{id}", update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var changed = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, changed.GetProperty("id").GetInt32());
        Assert.Equal(2, changed.GetProperty("dayOfWeek").GetInt32());
        Assert.Equal("11:00:00", changed.GetProperty("startTime").GetString());
        Assert.Equal("12:00:00", changed.GetProperty("endTime").GetString());
        Assert.Equal(2, changed.GetProperty("gradeLevel").GetInt32());
        Assert.Equal(changed.ToString(), (await (await client.GetAsync($"/api/schedule/{id}")).Content.ReadFromJsonAsync<JsonElement>()).ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/schedule/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/schedule/999", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/schedule/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/schedule/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/schedule/{id}")).StatusCode);
        var empty = await client.GetAsync("/api/schedule");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Invalid_and_duplicate_signups_do_not_change_credentials()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(client, "register", new { email = "invalid", password = "weak" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(client, "register", new { email = Email, password = "weakpassword" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Credentials(client, "register", new { email = Email, password = "AnotherStrong1!" })).StatusCode);
        var failed = await Credentials(client, "login", new { email = Email, password = "AnotherStrong1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.Contains("Invalid credentials or account unavailable.", await failed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Credentials(client, "login")).StatusCode);
        Assert.Equal(Email, (await (await client.GetAsync("/api/account/me")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Repeated_failures_lock_account()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Token(client);
        Assert.Equal(HttpStatusCode.Created, (await Credentials(client, "register")).StatusCode);
        for (var i = 0; i < 5; i++)
        {
            var failed = await Credentials(client, "login", new { email = Email, password = "WrongPassword1!" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            Assert.Contains("Invalid credentials or account unavailable.", await failed.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Credentials(client, "login")).StatusCode);
        await AssertApiFailure(await client.GetAsync("/api/account/me"), HttpStatusCode.Unauthorized);
    }
}
