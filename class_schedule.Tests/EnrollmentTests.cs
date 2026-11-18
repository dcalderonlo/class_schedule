using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace class_schedule.Tests;

public class EnrollmentTests
{
    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient c)
    {
        var token = await (await c.GetAsync("/api/account/csrf")).Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    private static async Task Login(WebApplicationFactory<Program> app, HttpClient c, string email, bool admin = false)
    {
        var credentials = new { email, password = "StrongPassword1!" }; await Token(c);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/account/register", credentials)).StatusCode);
        if (admin)
        {
            using var scope = app.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, SecurityPolicies.Administrator)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/account/login", credentials)).StatusCode); await Token(c);
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string message)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Single(body.EnumerateObject());
        Assert.Equal(message, body.GetProperty("error").GetString());
    }
    private static async Task SafeFailure(HttpResponseMessage response, EnrollmentApplicationFactory app)
    {
        Assert.True(app.NotificationFailureTriggered);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("An unexpected error occurred.", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("Injected notification failure", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqliteException", body, StringComparison.OrdinalIgnoreCase);
    }
    private static async Task<int> Add(HttpClient c, string resource, object body)
    {
        var response = await c.PostAsJsonAsync("/api/catalog/" + resource, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
    private static async Task<(int first, int second, int other)> Seed(HttpClient c)
    {
        int subject = await Add(c, "subjects", new { name = "Mathematics", code = "MATH", weeklyHours = 4 });
        int teacher = await Add(c, "teachers", new { firstName = "Ada", lastName = "Smith", email = "teacher@example.test" });
        int room = await Add(c, "rooms", new { number = "A", capacity = 30, type = 0 });
        int term = await Add(c, "terms", new { name = "Spring", startDate = "2026-01-05", endDate = "2026-05-01" });
        int offering = await Add(c, "offerings", new { subjectId = subject, academicTermId = term });
        int first = await Add(c, "sections", new { courseOfferingId = offering, code = "A", capacity = 2 });
        int second = await Add(c, "sections", new { courseOfferingId = offering, code = "B", capacity = 2 });
        int nextTerm = await Add(c, "terms", new { name = "Later", startDate = "2027-01-04", endDate = "2027-05-01" });
        int nextOffering = await Add(c, "offerings", new { subjectId = subject, academicTermId = nextTerm });
        int other = await Add(c, "sections", new { courseOfferingId = nextOffering, code = "A", capacity = 2 });
        foreach (var (section, day) in new[] { (first, 1), (second, 2), (other, 1) })
        {
            var response = await c.PostAsJsonAsync("/api/schedule", new { courseSectionId = section, teacherId = teacher, roomId = room, dayOfWeek = day, startTime = "09:00:00", endTime = "10:00:00" });
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }
        return (first, second, other);
    }
    private static async Task<string> UserId(HttpClient admin, string email)
    {
        var users = await (await admin.GetAsync("/api/enrollments/users")).Content.ReadFromJsonAsync<JsonElement>();
        return users.EnumerateArray().Single(x => x.GetProperty("email").GetString() == email).GetProperty("id").GetString()!;
    }

    private static async Task<(string enrollments, string timetable, string notices)> Snapshot(HttpClient client) =>
        (await client.GetStringAsync("/api/enrollments"), await client.GetStringAsync("/api/enrollments/timetable"), await client.GetStringAsync("/api/notifications"));

    private static async Task NotFound(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cross_offering_transfer_does_not_conflict_with_itself()
    {
        await using var app = new EnrollmentApplicationFactory();
        using var admin = Client(app); using var student = Client(app);
        await Login(app, admin, "admin@example.test", true);
        await Login(app, student, "student@example.test");
        var sections = await Seed(admin);
        var other = await OtherClass(admin, "10:00:00", "11:00:00");
        foreach (var section in new[] { sections.first, other.section })
            Assert.Equal(HttpStatusCode.Created,
                (await student.PostAsJsonAsync("/api/enrollments",
                    new { courseSectionId = section })).StatusCode);

        var meetings = await (await admin.GetAsync("/api/schedule"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var moving = meetings.EnumerateArray().Single(x =>
            x.GetProperty("courseSectionId").GetInt32() == sections.first);
        var id = moving.GetProperty("id").GetInt32();
        var before = await Snapshot(student);
        var response = await admin.PutAsJsonAsync($"/api/schedule/{id}", new {
            courseSectionId = other.section,
            teacherId = moving.GetProperty("teacherId").GetInt32(),
            roomId = moving.GetProperty("roomId").GetInt32(),
            dayOfWeek = 1, startTime = "09:00:00", endTime = "10:00:00"
        });
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Equal(before, await Snapshot(student));
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200; actual {(int)response.StatusCode}: {body}");
        Assert.Equal(before.enrollments,
            await student.GetStringAsync("/api/enrollments"));
        var timetable = await (await student.GetAsync("/api/enrollments/timetable"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, timetable.GetArrayLength());
        Assert.All(timetable.EnumerateArray(), x =>
            Assert.Equal(other.section, x.GetProperty("courseSectionId").GetInt32()));
        Assert.Single(timetable.EnumerateArray(),
            x => x.GetProperty("id").GetInt32() == id);
        var notices = await (await student.GetAsync("/api/notifications"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(notices.EnumerateArray());
        Assert.Equal("A meeting in your schedule has changed.",
            notices[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Shared_date_range_without_shared_weekday_and_inclusive_boundary()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test");
        async Task<int> Section(string label, string startDate, string endDate, string startTime, string endTime)
        {
            var subject = await Add(admin, "subjects", new { name = "Subject " + label, code = label, weeklyHours = 2 });
            var term = await Add(admin, "terms", new { name = "Term " + label, startDate, endDate });
            var teacher = await Add(admin, "teachers", new { firstName = "Teacher", lastName = label, email = label.ToLowerInvariant() + "@example.test" });
            var room = await Add(admin, "rooms", new { number = label, capacity = 5, type = 0 });
            var offering = await Add(admin, "offerings", new { subjectId = subject, academicTermId = term });
            var section = await Add(admin, "sections", new { courseOfferingId = offering, code = "A", capacity = 5 });
            var result = await admin.PostAsJsonAsync("/api/schedule", new { courseSectionId = section, teacherId = teacher, roomId = room, dayOfWeek = 1, startTime, endTime });
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            return section;
        }
        var first = await Section("FIRST", "2026-01-05", "2026-01-06", "09:00:00", "10:00:00");
        var second = await Section("SECOND", "2026-01-06", "2026-01-12", "09:00:00", "10:00:00");
        var conflict = await Section("THIRD", "2026-01-05", "2026-01-05", "09:00:00", "10:00:00");
        var adjacent = await Section("ADJACENT", "2026-01-05", "2026-01-05", "10:00:00", "11:00:00");
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = first })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = second })).StatusCode);
        var before = await Snapshot(student);
        await Error(await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = conflict }), HttpStatusCode.Conflict, "The section conflicts with an existing enrollment.");
        Assert.Equal(before, await Snapshot(student));
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = adjacent })).StatusCode);
        var enrollments = await (await student.GetAsync("/api/enrollments")).Content.ReadFromJsonAsync<JsonElement>();
        var timetable = await (await student.GetAsync("/api/enrollments/timetable")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, enrollments.GetArrayLength()); Assert.Equal(3, timetable.GetArrayLength());
        Assert.Contains(timetable.EnumerateArray(), item => item.GetProperty("courseSectionId").GetInt32() == adjacent);
        Assert.Empty((await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Error_contracts_are_safe_and_leave_own_state_unchanged()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app); using var outsider = Client(app);
        var anonymous = await student.GetAsync("/api/enrollments");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.Null(anonymous.Headers.Location);
        Assert.DoesNotContain("<html", await anonymous.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test"); await Login(app, outsider, "outsider@example.test");
        var sections = await Seed(admin);
        var forbidden = await student.GetAsync("/api/enrollments/users"); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Null(forbidden.Headers.Location);
        Assert.DoesNotContain("<html", await forbidden.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var before = await Snapshot(student);
        await Error(await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = -1 }), HttpStatusCode.BadRequest, "IDs must be positive.");
        Assert.Equal(before, await Snapshot(student));
        await Error(await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = 999999 }), HttpStatusCode.NotFound, "Enrollment or referenced record not found.");
        Assert.Equal(before, await Snapshot(student));
        var malformed = await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode); Assert.Equal("application/problem+json", malformed.Content.Headers.ContentType?.MediaType);
        var problem = await malformed.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("$.courseSectionId", out var errors)); Assert.NotEmpty(errors.EnumerateArray());
        Assert.Equal(before, await Snapshot(student));
        var created = await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32(); before = await Snapshot(student);
        await NotFound(await outsider.GetAsync($"/api/enrollments/{id}"));
        await Error(await outsider.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = sections.second }), HttpStatusCode.NotFound, "Enrollment or referenced record not found.");
        await NotFound(await outsider.PostAsync("/api/notifications/999999/read", null));
        await NotFound(await student.GetAsync("/api/enrollments/999999"));
        await Error(await student.PutAsJsonAsync("/api/enrollments/999999", new { courseSectionId = sections.second }), HttpStatusCode.NotFound, "Enrollment or referenced record not found.");
        Assert.Equal(before, await Snapshot(student));
    }

    [Fact]
    public async Task Admin_assignment_notifies_only_owner_and_read_is_idempotent()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app); using var other = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test"); await Login(app, other, "other@example.test");
        var sections = await Seed(admin); var studentId = await UserId(admin, "student@example.test");
        var assignment = await admin.PostAsJsonAsync($"/api/enrollments/users/{studentId}", new { courseSectionId = sections.first });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        int enrollmentId = (await assignment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var notifications = await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(notifications.EnumerateArray()); int notificationId = notifications[0].GetProperty("id").GetInt32();
        await NotFound(await admin.PostAsync($"/api/notifications/{notificationId}/read", null));
        await NotFound(await other.PostAsync($"/api/notifications/{notificationId}/read", null));
        var unread = await student.GetStringAsync("/api/notifications");
        student.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(unread, await student.GetStringAsync("/api/notifications"));
        await Token(student);
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode);
        var after = await student.GetStringAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(after, await student.GetStringAsync("/api/notifications"));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/enrollments/{enrollmentId}", new { courseSectionId = sections.second })).StatusCode);
        var updated = await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetArrayLength()); Assert.True(updated[0].GetProperty("id").GetInt32() > notificationId);
        Assert.Empty((await (await other.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/enrollments/{enrollmentId}", new { courseSectionId = sections.second })).StatusCode);
        Assert.Equal(updated.ToString(), (await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).ToString());
    }

    [Fact]
    public async Task Notification_failure_rolls_back_admin_assignment()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test");
        var sections = await Seed(admin); var studentId = await UserId(admin, "student@example.test");
        var beforeEnrollment = await student.GetStringAsync("/api/enrollments"); var beforeNotifications = await student.GetStringAsync("/api/notifications");
        var beforeSchedule = await student.GetStringAsync("/api/schedule");
        app.FailNotifications = true;
        HttpResponseMessage failure;
        try { failure = await admin.PostAsJsonAsync($"/api/enrollments/users/{studentId}", new { courseSectionId = sections.first }); }
        finally { app.FailNotifications = false; }
        await SafeFailure(failure, app);
        Assert.Equal(beforeEnrollment, await student.GetStringAsync("/api/enrollments"));
        Assert.Equal(beforeNotifications, await student.GetStringAsync("/api/notifications"));
        Assert.Equal(beforeSchedule, await student.GetStringAsync("/api/schedule"));
    }

    [Fact]
    public async Task Last_seat_is_protected_across_independent_connections()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var first = Client(app); using var second = Client(app); using var third = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, first, "first@example.test"); await Login(app, second, "second@example.test"); await Login(app, third, "third@example.test");
        var section = (await Seed(admin)).first;
        Assert.Equal(HttpStatusCode.Created, (await first.PostAsJsonAsync("/api/enrollments", new { courseSectionId = section })).StatusCode);
        var results = await Task.WhenAll(second.PostAsJsonAsync("/api/enrollments", new { courseSectionId = section }), third.PostAsJsonAsync("/api/enrollments", new { courseSectionId = section }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    private static async Task<(int section, int teacher, int room, int meeting)> OtherClass(HttpClient admin, string start, string end)
    {
        var terms = await (await admin.GetAsync("/api/catalog/terms")).Content.ReadFromJsonAsync<JsonElement>();
        var term = terms[0].GetProperty("id").GetInt32();
        var subject = await Add(admin, "subjects", new { name = "Physics", code = "PHY", weeklyHours = 4 });
        var teacher = await Add(admin, "teachers", new { firstName = "Grace", lastName = "Smith", email = "physics@example.test" });
        var room = await Add(admin, "rooms", new { number = "B", capacity = 30, type = 0 });
        var offering = await Add(admin, "offerings", new { subjectId = subject, academicTermId = term });
        var section = await Add(admin, "sections", new { courseOfferingId = offering, code = "A", capacity = 2 });
        var response = await admin.PostAsJsonAsync("/api/schedule", new { courseSectionId = section, teacherId = teacher, roomId = room, dayOfWeek = 1, startTime = start, endTime = end });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (section, teacher, room, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Meeting_edits_preserve_student_conflicts_and_notify_only_affected()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app); using var outsider = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test"); await Login(app, outsider, "outsider@example.test");
        var sections = await Seed(admin); var other = await OtherClass(admin, "10:00:00", "11:00:00");
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = other.section })).StatusCode);
        var beforeSchedules = await admin.GetStringAsync("/api/schedule"); var beforeNotices = await student.GetStringAsync("/api/notifications");
        var conflict = new { courseSectionId = other.section, teacherId = other.teacher, roomId = other.room, dayOfWeek = 1, startTime = "09:30:00", endTime = "10:30:00" };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/schedule/{other.meeting}", conflict)).StatusCode);
        Assert.Equal(beforeSchedules, await admin.GetStringAsync("/api/schedule")); Assert.Equal(beforeNotices, await student.GetStringAsync("/api/notifications"));
        var valid = new { courseSectionId = other.section, teacherId = other.teacher, roomId = other.room, dayOfWeek = 1, startTime = "11:00:00", endTime = "12:00:00" };
        app.FailNotifications = true;
        var beforeEnrollments = await student.GetStringAsync("/api/enrollments");
        HttpResponseMessage failure;
        try { failure = await admin.PutAsJsonAsync($"/api/schedule/{other.meeting}", valid); }
        finally { app.FailNotifications = false; }
        await SafeFailure(failure, app);
        Assert.Equal(beforeEnrollments, await student.GetStringAsync("/api/enrollments"));
        Assert.Equal(beforeSchedules, await admin.GetStringAsync("/api/schedule")); Assert.Equal(beforeNotices, await student.GetStringAsync("/api/notifications"));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/schedule/{other.meeting}", valid)).StatusCode);
        var notices = await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Single(notices.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/schedule/{other.meeting}", valid)).StatusCode);
        Assert.Equal(notices.ToString(), (await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).ToString());
        Assert.Empty((await (await outsider.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        var additional = new { courseSectionId = other.section, teacherId = other.teacher, roomId = other.room, dayOfWeek = 2, startTime = "11:00:00", endTime = "12:00:00" };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/schedule", additional)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/schedule/{other.meeting}")).StatusCode);
        Assert.Equal(3, (await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task Meeting_transfer_notifies_distinct_users_in_old_and_new_sections()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var first = Client(app); using var second = Client(app); using var outsider = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, first, "first@example.test"); await Login(app, second, "second@example.test"); await Login(app, outsider, "outsider@example.test");
        var sections = await Seed(admin);
        Assert.Equal(HttpStatusCode.Created, (await first.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.second })).StatusCode);
        var meetings = await (await admin.GetAsync("/api/schedule")).Content.ReadFromJsonAsync<JsonElement>();
        var meeting = meetings.EnumerateArray().Single(x => x.GetProperty("courseSectionId").GetInt32() == sections.first);
        int id = meeting.GetProperty("id").GetInt32();
        var move = new { courseSectionId = sections.second, teacherId = meeting.GetProperty("teacherId").GetInt32(), roomId = meeting.GetProperty("roomId").GetInt32(), dayOfWeek = 1, startTime = "09:00:00", endTime = "10:00:00" };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/schedule/{id}", move)).StatusCode);
        Assert.Single((await (await first.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Single((await (await second.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Empty((await (await outsider.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Selection_requires_meetings_and_respects_term_boundaries_and_admin_policy()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test");
        var sections = await Seed(admin); var users = await (await admin.GetAsync("/api/enrollments/users")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(users.EnumerateArray(), user => Assert.Equal(new[] { "id", "email" }, user.EnumerateObject().Select(x => x.Name).ToArray()));
        var studentId = await UserId(admin, "student@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync($"/api/enrollments/users/{studentId}", new { courseSectionId = sections.first })).StatusCode);
        var sectionList = await (await admin.GetAsync("/api/catalog/sections")).Content.ReadFromJsonAsync<JsonElement>();
        var offering = sectionList.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == sections.first).GetProperty("courseOfferingId").GetInt32();
        int empty = await Add(admin, "sections", new { courseOfferingId = offering, code = "EMPTY", capacity = 2 });
        var before = await Snapshot(student);
        await Error(await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = empty }), HttpStatusCode.Conflict, "The section has no scheduled meetings.");
        Assert.Equal(before, await Snapshot(student));
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.other })).StatusCode);
        Assert.Equal(2, (await (await student.GetAsync("/api/enrollments/timetable")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
        var beforePaging = await Snapshot(student);
        foreach (var query in new[] { "page=0", "pageSize=101", "page=2147483647&pageSize=100" })
        {
            await Error(await student.GetAsync("/api/enrollments?" + query), HttpStatusCode.BadRequest, "Invalid pagination bounds.");
            await Error(await student.GetAsync("/api/notifications?" + query), HttpStatusCode.BadRequest, "Invalid pagination bounds.");
            Assert.Equal(beforePaging, await Snapshot(student));
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/enrollments/users/{studentId}")).StatusCode);
    }

    [Fact]
    public async Task Conflict_and_full_switch_leave_all_own_representations_unchanged()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app); using var one = Client(app); using var two = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test"); await Login(app, one, "one@example.test"); await Login(app, two, "two@example.test");
        var sections = await Seed(admin); var other = await OtherClass(admin, "09:00:00", "10:00:00");
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.second })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = other.section })).StatusCode);
        var list = await (await student.GetAsync("/api/enrollments")).Content.ReadFromJsonAsync<JsonElement>();
        int id = list.EnumerateArray().Single(x => x.GetProperty("courseSectionId").GetInt32() == sections.second).GetProperty("id").GetInt32();
        var beforeList = await student.GetStringAsync("/api/enrollments"); var beforeTimetable = await student.GetStringAsync("/api/enrollments/timetable"); var beforeNotices = await student.GetStringAsync("/api/notifications");
        await Error(await student.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = sections.first }), HttpStatusCode.Conflict, "The section conflicts with an existing enrollment.");
        Assert.Equal(beforeList, await student.GetStringAsync("/api/enrollments")); Assert.Equal(beforeTimetable, await student.GetStringAsync("/api/enrollments/timetable")); Assert.Equal(beforeNotices, await student.GetStringAsync("/api/notifications"));
        Assert.Equal(HttpStatusCode.Created, (await one.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await two.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first })).StatusCode);
        await Error(await student.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = sections.first }), HttpStatusCode.Conflict, "The section is full.");
        Assert.Equal(beforeList, await student.GetStringAsync("/api/enrollments")); Assert.Equal(beforeTimetable, await student.GetStringAsync("/api/enrollments/timetable")); Assert.Equal(beforeNotices, await student.GetStringAsync("/api/notifications"));
    }

    [Fact]
    public async Task Incompatible_concurrent_choices_for_one_user_cannot_both_succeed()
    {
        await using var app = new EnrollmentApplicationFactory(); using var admin = Client(app); using var student = Client(app);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test");
        var sections = await Seed(admin); var other = await OtherClass(admin, "09:30:00", "10:30:00");
        var results = await Task.WhenAll(student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = sections.first }), student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = other.section }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await (await student.GetAsync("/api/enrollments")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Enrollment_is_owned_and_switch_is_atomic()
    {
        await using var app = new AuthenticationApplicationFactory(); using var admin = Client(app); using var student = Client(app); using var outsider = Client(app);
        Assert.Equal(HttpStatusCode.Unauthorized, (await student.GetAsync("/api/enrollments")).StatusCode);
        await Login(app, admin, "admin@example.test", true); await Login(app, student, "student@example.test"); await Login(app, outsider, "outsider@example.test");
        var s = await Seed(admin);
        var created = await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = s.first, userId = "forged", role = "Administrator" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        int id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/enrollments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/enrollments/users")).StatusCode);
        var before = await Snapshot(student);
        await Error(await student.PostAsJsonAsync("/api/enrollments", new { courseSectionId = s.second }), HttpStatusCode.Conflict, "An enrollment already exists for this offering.");
        Assert.Equal(before, await Snapshot(student));
        await Error(await student.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = s.other }), HttpStatusCode.Conflict, "Switches must stay within the same offering.");
        Assert.Equal(before, await Snapshot(student));
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = s.second })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync($"/api/enrollments/{id}", new { courseSectionId = s.second })).StatusCode);
        var list = await (await student.GetAsync("/api/enrollments")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(list.EnumerateArray()); Assert.Equal(id, list[0].GetProperty("id").GetInt32()); Assert.Equal(s.second, list[0].GetProperty("courseSectionId").GetInt32());
        Assert.Single((await (await student.GetAsync("/api/enrollments/timetable")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Empty((await (await student.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }
}
