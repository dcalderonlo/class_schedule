using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace class_schedule.Tests;

public class StartupTests
{
    [Fact]
    public async Task Home_remains_available_with_project_stylesheet()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("class_schedule", html);
        Assert.DoesNotContain("BlazorApp1", html);
    }
}
