using class_schedule.Application.Security;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace class_schedule.Infrastructure.Identity;

public static class IdentityRegistration
{
    public static IServiceCollection AddScheduleIdentity(this IServiceCollection services)
    {
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        }).AddEntityFrameworkStores<ClassScheduleDbContext>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-class-schedule";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-class-schedule-csrf";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddAuthorization(options => options.AddPolicy(SecurityPolicies.Administrator,
            policy => policy.RequireRole(SecurityPolicies.Administrator)));
        return services;
    }
}
