using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace class_schedule.Tests;

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLASS_SCHEDULE_SQLSERVER_TEST_ENABLED") != "1")
            Skip = "SQL Server verification requires explicit opt-in and a dedicated disposable instance.";
    }
}

public class SqlServerIntegrationTests
{
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var token = await (await client.GetAsync("/api/account/csrf")).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    private static async Task Login(HttpClient client, string email, bool register)
    {
        var credentials = new { email, password = SqlServerApplicationFactory.Password };
        await Token(client);
        if (register) Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", credentials)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", credentials)).StatusCode);
        await Token(client);
    }
    private static async Task<int> Add(HttpClient client, string resource, object body)
    {
        var result = await client.PostAsJsonAsync("/api/catalog/" + resource, body);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
    private static async Task<(int section, int teacher, int room, int meeting)> Section(HttpClient admin, int term, string label, int capacity, string start, string end)
    {
        var subject = await Add(admin, "subjects", new { name = label, code = label, weeklyHours = 4 });
        var teacher = await Add(admin, "teachers", new { firstName = "Test", lastName = label, email = label + "@example.test" });
        var room = await Add(admin, "rooms", new { number = label, capacity = 10, type = 0 });
        var offering = await Add(admin, "offerings", new { subjectId = subject, academicTermId = term });
        var section = await Add(admin, "sections", new { courseOfferingId = offering, code = "A", capacity });
        var response = await admin.PostAsJsonAsync("/api/schedule", new { courseSectionId = section, teacherId = teacher, roomId = room, dayOfWeek = 1, startTime = start, endTime = end });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (section, teacher, room, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
    }
    private static async Task<(string meetings, string enrollment, string notifications)> Snapshot(HttpClient admin, HttpClient student) =>
        (await admin.GetStringAsync("/api/schedule"), await student.GetStringAsync("/api/enrollments"), await student.GetStringAsync("/api/notifications"));

    [SqlServerFact]
    public async Task Real_provider_migration_bootstrap_and_enrollment_workflow()
    {
        var connection = SqlServerTestDatabase.RequireConnection(Environment.GetEnvironmentVariable("CLASS_SCHEDULE_SQLSERVER_TEST_ENABLED"), Environment.GetEnvironmentVariable("CLASS_SCHEDULE_SQLSERVER_TEST_CONNECTION"));
        await using var database = await SqlServerTestDatabase.CreateAsync(connection);
        var options = new DbContextOptionsBuilder<ClassScheduleDbContext>().UseSqlServer(database.ConnectionString).Options;
        await using (var db = new ClassScheduleDbContext(options))
            Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("_InitialCreate", StringComparison.Ordinal));
        await using (var bootstrap = new SqlServerApplicationFactory(database.ConnectionString, true))
        using (var client = Client(bootstrap))
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/csrf")).StatusCode);
        await using var app = new SqlServerApplicationFactory(database.ConnectionString, false);
        using var admin = Client(app); using var one = Client(app); using var two = Client(app); using var other = Client(app);
        await Login(admin, SqlServerApplicationFactory.AdminEmail, false);
        await Login(one, "one@example.test", true); await Login(two, "two@example.test", true); await Login(other, "other@example.test", true);
        var term = await Add(admin, "terms", new { name = "Spring", startDate = "2026-01-05", endDate = "2026-05-01" });
        var seat = await Section(admin, term, "SEAT", 1, "09:00:00", "10:00:00");
        var results = await Task.WhenAll(one.PostAsJsonAsync("/api/enrollments", new { courseSectionId = seat.section }), two.PostAsJsonAsync("/api/enrollments", new { courseSectionId = seat.section }));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var owner = results[0].StatusCode == HttpStatusCode.Created ? one : two;
        var outsider = ReferenceEquals(owner, one) ? two : one;
        var enrollment = (await (await owner.GetAsync("/api/enrollments")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/enrollments/{enrollment}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/enrollments/users")).StatusCode);
        var a = await Section(admin, term, "FIRST", 5, "11:00:00", "12:00:00");
        var b = await Section(admin, term, "SECOND", 5, "11:30:00", "12:30:00");
        var choices = await Task.WhenAll(other.PostAsJsonAsync("/api/enrollments", new { courseSectionId = a.section }), other.PostAsJsonAsync("/api/enrollments", new { courseSectionId = b.section }));
        Assert.Single(choices, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(choices, r => r.StatusCode == HttpStatusCode.Conflict);
        var move = new { courseSectionId = seat.section, teacherId = seat.teacher, roomId = seat.room, dayOfWeek = 1, startTime = "08:00:00", endTime = "09:00:00" };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/schedule/{seat.meeting}", move)).StatusCode);
        var notices = await (await owner.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(notices.EnumerateArray());
        Assert.Equal("A meeting in your schedule has changed.", notices[0].GetProperty("message").GetString());
        Assert.Empty((await (await outsider.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        var before = await Snapshot(admin, owner);
        app.FailNotifications = true;
        HttpResponseMessage failure;
        try { failure = await admin.PutAsJsonAsync($"/api/schedule/{seat.meeting}", new { courseSectionId = seat.section, teacherId = seat.teacher, roomId = seat.room, dayOfWeek = 1, startTime = "07:00:00", endTime = "08:00:00" }); }
        finally { app.FailNotifications = false; }
        Assert.True(app.FailureTriggered); Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Equal("application/json", failure.Content.Headers.ContentType?.MediaType);
        Assert.Equal("An unexpected error occurred.", (await failure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(before, await Snapshot(admin, owner));
    }
}
