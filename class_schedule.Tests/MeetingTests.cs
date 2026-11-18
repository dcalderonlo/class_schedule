using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace class_schedule.Tests;

public class MeetingTests
{
    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var json = await (await client.GetAsync("/api/account/csrf")).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task Login(WebApplicationFactory<Program> app, HttpClient client, bool register = true)
    {
        var credentials = new { email = "meeting@example.test", password = "StrongPassword1!" };
        await Token(client);
        if (register)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", credentials)).StatusCode);
            using var scope = app.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(credentials.email))!, SecurityPolicies.Administrator)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", credentials)).StatusCode);
        await Token(client);
    }
    private static async Task<int> Add(HttpClient client, string resource, object data)
    {
        var response = await client.PostAsJsonAsync("/api/catalog/" + resource, data);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
    private static async Task<(int section, int other, int later, int teacher, int teacher2, int room, int room2)> Seed(HttpClient c, int hours = 4)
    {
        var subject = await Add(c, "subjects", new { name = "Math", code = "MAT", weeklyHours = hours });
        var teacher = await Add(c, "teachers", new { firstName = "Ada", lastName = "Lovelace", email = "ada@example.test" });
        var teacher2 = await Add(c, "teachers", new { firstName = "Grace", lastName = "Hopper", email = "grace@example.test" });
        var room = await Add(c, "rooms", new { number = "A", capacity = 30, type = 0 });
        var room2 = await Add(c, "rooms", new { number = "B", capacity = 30, type = 0 });
        var term = await Add(c, "terms", new { name = "Spring", startDate = "2026-01-05", endDate = "2026-05-01" });
        var laterTerm = await Add(c, "terms", new { name = "Later", startDate = "2027-01-04", endDate = "2027-05-01" });
        var offering = await Add(c, "offerings", new { subjectId = subject, academicTermId = term });
        var laterOffering = await Add(c, "offerings", new { subjectId = subject, academicTermId = laterTerm });
        var section = await Add(c, "sections", new { courseOfferingId = offering, code = "A", capacity = 20 });
        var other = await Add(c, "sections", new { courseOfferingId = offering, code = "B", capacity = 20 });
        var later = await Add(c, "sections", new { courseOfferingId = laterOffering, code = "A", capacity = 20 });
        return (section, other, later, teacher, teacher2, room, room2);
    }
    private static object Meeting(int section, int teacher, int room, int day = 1, string start = "09:00:00", string end = "10:00:00") =>
        new { courseSectionId = section, teacherId = teacher, roomId = room, dayOfWeek = day, startTime = start, endTime = end };
    private static async Task<int> Create(HttpClient c, object meeting)
    {
        var response = await c.PostAsJsonAsync("/api/schedule", meeting);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
    private static async Task Reject(HttpClient c, object meeting, HttpStatusCode status, int? id = null)
    {
        var before = await c.GetStringAsync("/api/schedule?pageSize=100");
        var result = id is null ? await c.PostAsJsonAsync("/api/schedule", meeting) : await c.PutAsJsonAsync($"/api/schedule/{id}", meeting);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(before, await c.GetStringAsync("/api/schedule?pageSize=100"));
    }
    [Fact]
    public async Task Crud_conflicts_capacity_hours_and_validation()
    {
        await using var app = new AuthenticationApplicationFactory(); using var c = Client(app); await Login(app, c);
        var s = await Seed(c, 2);
        var first = await Create(c, Meeting(s.section, s.teacher, s.room));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/schedule/{first}")).StatusCode);
        await Create(c, Meeting(s.other, s.teacher2, s.room2));
        // A different section isolates each resource rule from the section rule.
        var firstItem = await (await c.GetAsync($"/api/schedule/{first}")).Content.ReadFromJsonAsync<JsonElement>();
        var third = await Add(c, "sections", new { courseOfferingId = firstItem.GetProperty("courseOfferingId").GetInt32(), code = "C", capacity = 20 });
        var thirdTeacher = await Add(c, "teachers", new { firstName = "Katherine", lastName = "Johnson", email = "kj@example.test" });
        var thirdRoom = await Add(c, "rooms", new { number = "C", capacity = 30, type = 0 });
        await Reject(c, Meeting(third, thirdTeacher, s.room), HttpStatusCode.Conflict);
        await Reject(c, Meeting(third, s.teacher, thirdRoom), HttpStatusCode.Conflict);
        await Reject(c, Meeting(s.section, s.teacher2, s.room2), HttpStatusCode.Conflict);
        await Create(c, Meeting(s.later, s.teacher, s.room));
        var adjacent = await Create(c, Meeting(s.section, s.teacher, s.room, start: "10:00:00", end: "11:00:00"));
        await Reject(c, Meeting(s.section, s.teacher, s.room, 2), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/schedule/{first}", Meeting(s.section, s.teacher, s.room))).StatusCode);
        await Reject(c, Meeting(s.section, s.teacher, s.room, 2, "00:00:00", "02:00:00"), HttpStatusCode.Conflict, first);
        await Reject(c, Meeting(s.section, s.teacher, s.room, 9), HttpStatusCode.BadRequest);
        await Reject(c, Meeting(s.section, s.teacher, s.room, 1, "23:00:00", "01:00:00"), HttpStatusCode.BadRequest);
        await Reject(c, Meeting(s.section, s.teacher, s.room, 1, "01:00:00", "01:00:00"), HttpStatusCode.BadRequest);
        await Reject(c, Meeting(0, s.teacher, s.room), HttpStatusCode.BadRequest);
        await Reject(c, Meeting(999, s.teacher, s.room), HttpStatusCode.NotFound);
        await Reject(c, Meeting(s.section, 999, s.room), HttpStatusCode.NotFound);
        await Reject(c, Meeting(s.section, s.teacher, 999), HttpStatusCode.NotFound);
        await Reject(c, Meeting(s.section, -1, s.room), HttpStatusCode.BadRequest);
        var small = await Add(c, "rooms", new { number = "Small", capacity = 1, type = 0 });
        await Reject(c, Meeting(s.section, s.teacher, small, 2), HttpStatusCode.Conflict);
        await Reject(c, Meeting(s.section, s.teacher, small, 2), HttpStatusCode.Conflict, first);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/schedule/{adjacent}")).StatusCode);
        await Create(c, Meeting(s.section, s.teacher, s.room, 2, "00:00:00", "01:00:00"));
    }
    [Fact]
    public async Task List_rejects_invalid_and_overflowing_pagination()
    {
        await using var app = new AuthenticationApplicationFactory(); using var c = Client(app); await Login(app, c);
        var before = await c.GetStringAsync("/api/schedule");
        foreach (var query in new[] { "page=0", "pageSize=101", "page=2147483647&pageSize=100" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/schedule?" + query)).StatusCode);
            Assert.Equal(before, await c.GetStringAsync("/api/schedule"));
        }
    }
    [Fact]
    public async Task Weekday_intersection_and_large_hours_are_safe()
    {
        await using var app = new AuthenticationApplicationFactory(); using var c = Client(app); await Login(app, c);
        var s = await Seed(c, int.MaxValue);
        var subject = await Add(c, "subjects", new { name = "Other", code = "OTHER", weeklyHours = 4 });
        var t1 = await Add(c, "terms", new { name = "First", startDate = "2026-01-05", endDate = "2026-01-06" });
        var t2 = await Add(c, "terms", new { name = "Second", startDate = "2026-01-06", endDate = "2026-01-12" });
        var t3 = await Add(c, "terms", new { name = "Third", startDate = "2026-01-05", endDate = "2026-01-05" });
        var o1 = await Add(c, "offerings", new { subjectId = subject, academicTermId = t1 });
        var o2 = await Add(c, "offerings", new { subjectId = subject, academicTermId = t2 });
        var o3 = await Add(c, "offerings", new { subjectId = subject, academicTermId = t3 });
        var a = await Add(c, "sections", new { courseOfferingId = o1, code = "A", capacity = 10 });
        var b = await Add(c, "sections", new { courseOfferingId = o2, code = "A", capacity = 10 });
        var d = await Add(c, "sections", new { courseOfferingId = o3, code = "A", capacity = 10 });
        await Create(c, Meeting(a, s.teacher, s.room));
        await Create(c, Meeting(b, s.teacher, s.room));
        await Reject(c, Meeting(d, s.teacher, s.room), HttpStatusCode.Conflict);
        await Reject(c, Meeting(d, s.teacher, s.room, 2), HttpStatusCode.BadRequest);
        await Create(c, Meeting(s.section, s.teacher2, s.room2, 2, "00:00:00", "23:59:00"));
    }

    [Fact]
    public async Task Paging_is_bounded_ordered_and_stable()
    {
        await using var app = new AuthenticationApplicationFactory(); using var c = Client(app); await Login(app, c);
        var s = await Seed(c, 30);
        for (var hour = 0; hour < 21; hour++)
            await Create(c, Meeting(s.section, s.teacher, s.room, 1, $"{hour:D2}:00:00", $"{hour + 1:D2}:00:00"));
        var first = await (await c.GetAsync("/api/schedule")).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await c.GetAsync("/api/schedule?page=2")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, first.GetArrayLength()); Assert.Single(second.EnumerateArray());
        Assert.True(first[19].GetProperty("id").GetInt32() < second[0].GetProperty("id").GetInt32());
        Assert.Equal(first.ToString(), (await (await c.GetAsync("/api/schedule")).Content.ReadFromJsonAsync<JsonElement>()).ToString());
        Assert.Equal(21, (await (await c.GetAsync("/api/schedule?pageSize=100")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task Concurrent_weekly_load_writes_allow_at_most_one()
    {
        await using var app = new MeetingApplicationFactory(); using var a = Client(app); using var b = Client(app);
        await Login(app, a); await Login(app, b, false); var s = await Seed(a, 3);
        var responses = await Task.WhenAll(a.PostAsJsonAsync("/api/schedule", Meeting(s.section, s.teacher, s.room, 1, "09:00:00", "11:00:00")), b.PostAsJsonAsync("/api/schedule", Meeting(s.section, s.teacher2, s.room2, 2, "09:00:00", "11:00:00")));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await (await a.GetAsync("/api/schedule")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_writes_use_independent_connections()
    {
        await using var app = new MeetingApplicationFactory(); using var a = Client(app); using var b = Client(app);
        await Login(app, a); await Login(app, b, false); var s = await Seed(a);
        var responses = await Task.WhenAll(a.PostAsJsonAsync("/api/schedule", Meeting(s.section, s.teacher, s.room)), b.PostAsJsonAsync("/api/schedule", Meeting(s.other, s.teacher, s.room2)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await (await a.GetAsync("/api/schedule")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }
}
