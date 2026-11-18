using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace class_schedule.Tests;

public class NotificationTests
{
    [Fact]
    public async Task Own_empty_list_requires_authentication_and_has_bounded_paging()
    {
        await using var app = new AuthenticationApplicationFactory();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
        var token = await (await client.GetAsync("/api/account/csrf")).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        var credentials = new { email = "notifications@example.test", password = "StrongPassword1!" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", credentials)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", credentials)).StatusCode);
        var response = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        foreach (var query in new[] { "page=0", "pageSize=101", "page=2147483647&pageSize=100" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notifications?" + query)).StatusCode);
    }
}
