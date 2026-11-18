using class_schedule.Domain.Models.Entities;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace class_schedule.Tests;

internal sealed class SqlServerApplicationFactory(string connection, bool bootstrap) : WebApplicationFactory<Program>
{
    internal const string AdminEmail = "admin@example.test";
    internal const string Password = "StrongPassword1!";
    internal bool FailNotifications { get; set; }
    internal bool FailureTriggered { get; private set; }

    private sealed class FailureInterceptor(SqlServerApplicationFactory factory) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (factory.FailNotifications && data.Context!.ChangeTracker.Entries<UserNotification>().Any(x => x.State == EntityState.Added))
            {
                factory.FailureTriggered = true;
                throw new InvalidOperationException("Injected notification failure.");
            }
            return base.SavingChangesAsync(data, result, ct);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(bootstrap ? "Development" : "Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ClassSchedule"] = connection,
            ["DevelopmentAdmin:Enabled"] = bootstrap ? "true" : "false",
            ["DevelopmentAdmin:Email"] = AdminEmail,
            ["DevelopmentAdmin:Password"] = Password
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddDbContext<ClassScheduleDbContext>(o => o.AddInterceptors(new FailureInterceptor(this)));
        });
    }
}
