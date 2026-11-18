using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace class_schedule.Tests;

public class AcademicCatalogTests
{
    private static HttpClient Client(AuthenticationApplicationFactory app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var body = await (await client.GetAsync("/api/account/csrf")).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
    }
    private static async Task Login(AuthenticationApplicationFactory app, HttpClient client, bool admin)
    {
        await Token(client);
        var credentials = new { email = "student@example.test", password = "StrongPassword1!" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", credentials)).StatusCode);
        if (admin)
        {
            using var scope = app.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(credentials.email))!, SecurityPolicies.Administrator)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", credentials)).StatusCode);
        await Token(client);
    }
    private static async Task<JsonElement> Create(HttpClient client, string resource, object data)
    {
        var response = await client.PostAsJsonAsync("/api/catalog/" + resource, data);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task Reject(HttpClient client, string resource, object data, HttpStatusCode status)
    {
        var before = await client.GetStringAsync("/api/catalog/" + resource);
        var response = await client.PostAsJsonAsync("/api/catalog/" + resource, data);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
        Assert.Equal(before, await client.GetStringAsync("/api/catalog/" + resource));
    }
    [Fact]
    public async Task Authenticated_student_can_read_empty_subject_catalog()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Login(app, client, false);
        var response = await client.GetAsync("/api/catalog/subjects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }
    [Fact]
    public async Task Creates_catalog_and_rejects_invalid_and_duplicate_records()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        await Login(app, client, true);
        var subject = await Create(client, "subjects", new { name = " Mathematics ", code = " mat ", weeklyHours = 4 });
        Assert.Equal("MAT", subject.GetProperty("code").GetString());
        await Reject(client, "subjects", new { name = "Math", code = "MAT", weeklyHours = 4 }, HttpStatusCode.Conflict);
        await Reject(client, "subjects", new { name = "Math", code = "M2", weeklyHours = 0 }, HttpStatusCode.BadRequest);
        var teacher = await Create(client, "teachers", new { firstName = " Ada ", lastName = " Lovelace ", email = "ada@example.test" });
        Assert.Equal("Ada Lovelace", teacher.GetProperty("name").GetString());
        Assert.False(teacher.TryGetProperty("email", out _));
        await Reject(client, "teachers", new { firstName = "A", lastName = "B", email = "ADA@EXAMPLE.TEST" }, HttpStatusCode.Conflict);
        await Reject(client, "teachers", new { firstName = " ", lastName = "B", email = "bad" }, HttpStatusCode.BadRequest);
        var room = await Create(client, "rooms", new { number = " A1 ", capacity = 30, type = 0 });
        Assert.Equal("A1", room.GetProperty("number").GetString());
        await Reject(client, "rooms", new { number = "A1", capacity = 30, type = 0 }, HttpStatusCode.Conflict);
        await Reject(client, "rooms", new { number = "B1", capacity = 0, type = 0 }, HttpStatusCode.BadRequest);
        await Reject(client, "rooms", new { number = "B1", capacity = 1, type = 99 }, HttpStatusCode.BadRequest);
        var term = await Create(client, "terms", new { name = "Spring", startDate = "2026-01-01", endDate = "2026-05-01" });
        await Create(client, "terms", new { name = "Spring", startDate = "2027-01-01", endDate = "2027-05-01" });
        await Reject(client, "terms", new { name = "Bad", startDate = "2026-05-01", endDate = "2026-01-01" }, HttpStatusCode.BadRequest);
        await Reject(client, "terms", new { name = " ", startDate = "2026-01-01", endDate = "2026-01-01" }, HttpStatusCode.BadRequest);
        await Reject(client, "subjects", new { name = "Math", code = new string('x', 21), weeklyHours = 1 }, HttpStatusCode.BadRequest);
        var sid = subject.GetProperty("id").GetInt32(); var tid = term.GetProperty("id").GetInt32();
        var offering = await Create(client, "offerings", new { subjectId = sid, academicTermId = tid });
        await Reject(client, "offerings", new { subjectId = sid, academicTermId = tid }, HttpStatusCode.Conflict);
        await Reject(client, "offerings", new { subjectId = 0, academicTermId = tid }, HttpStatusCode.BadRequest);
        await Reject(client, "offerings", new { subjectId = 999, academicTermId = tid }, HttpStatusCode.NotFound);
        var oid = offering.GetProperty("id").GetInt32();
        var section = await Create(client, "sections", new { courseOfferingId = oid, code = " a ", capacity = 20 });
        Assert.Equal("A", section.GetProperty("code").GetString());
        await Reject(client, "sections", new { courseOfferingId = oid, code = "a", capacity = 20 }, HttpStatusCode.Conflict);
        await Reject(client, "sections", new { courseOfferingId = oid, code = "b", capacity = 0 }, HttpStatusCode.BadRequest);
        await Reject(client, "sections", new { courseOfferingId = 999, code = "b", capacity = 20 }, HttpStatusCode.NotFound);
        await Reject(client, "offerings", new { subjectId = sid, academicTermId = 999 }, HttpStatusCode.NotFound);
        var otherTerm = await Create(client, "terms", new { name = "Another", startDate = "2028-01-01", endDate = "2028-01-01" });
        var otherOffering = await Create(client, "offerings", new { subjectId = sid, academicTermId = otherTerm.GetProperty("id").GetInt32() });
        await Create(client, "sections", new { courseOfferingId = otherOffering.GetProperty("id").GetInt32(), code = "a", capacity = 1 });
        await Reject(client, "rooms", new { number = " a1 ", capacity = 1, type = 0 }, HttpStatusCode.Conflict);
        await Reject(client, "subjects", new { name = "x", code = " mat ", weeklyHours = 1 }, HttpStatusCode.Conflict);
        foreach (var resource in new[] { "subjects", "teachers", "rooms", "terms", "offerings", "sections" })
            Assert.NotEmpty((await (await client.GetAsync("/api/catalog/" + resource)).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }
    [Fact]
    public async Task Pagination_is_bounded_and_ordered()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app); await Login(app, client, true);
        for (int i = 0; i < 3; i++) await Create(client, "subjects", new { name = "Subject", code = "C" + i, weeklyHours = 1 });
        var first = await (await client.GetAsync("/api/catalog/subjects?page=1&pageSize=2")).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await client.GetAsync("/api/catalog/subjects?page=2&pageSize=2")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, first.GetArrayLength()); Assert.Single(second.EnumerateArray());
        Assert.True(first[0].GetProperty("id").GetInt32() < first[1].GetProperty("id").GetInt32());
        Assert.True(first[1].GetProperty("id").GetInt32() < second[0].GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/subjects?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/subjects?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/subjects?page=2147483647&pageSize=100")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/catalog/subjects?page=99999999999999999999")).StatusCode);
        for (int i = 3; i < 23; i++) await Create(client, "subjects", new { name = "Subject", code = "C" + i, weeklyHours = 1 });
        Assert.Equal(20, (await (await client.GetAsync("/api/catalog/subjects")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
        Assert.Equal(23, (await (await client.GetAsync("/api/catalog/subjects?pageSize=100")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }
    [Fact]
    public async Task Authorization_and_csrf_rejections_do_not_change_catalog()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = Client(app);
        foreach (var resource in new[] { "subjects", "teachers", "rooms", "terms", "offerings", "sections" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/catalog/" + resource)).StatusCode);
        await Token(client);
        var data = new { name = "Math", code = "MAT", weeklyHours = 1 };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/catalog/subjects", data)).StatusCode);
        await Login(app, client, false);
        var before = await client.GetStringAsync("/api/catalog/subjects");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/catalog/subjects", data)).StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/catalog/subjects"));
        await using var adminApp = new AuthenticationApplicationFactory(); using var admin = Client(adminApp);
        await Login(adminApp, admin, true);
        before = await admin.GetStringAsync("/api/catalog/subjects");
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalog/subjects", data)).StatusCode);
        Assert.Equal(before, await admin.GetStringAsync("/api/catalog/subjects"));
    }
}
