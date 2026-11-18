using class_schedule.Domain.Models.Entities;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace class_schedule.Tests;

public sealed class EnrollmentApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "enrollment-tests-" + Guid.NewGuid());
    public bool FailNotifications { get; set; }
    public bool NotificationFailureTriggered { get; private set; }
    private sealed class FailureInterceptor(EnrollmentApplicationFactory factory) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (factory.FailNotifications && data.Context!.ChangeTracker.Entries<UserNotification>().Any(x => x.State == EntityState.Added))
            {
                factory.NotificationFailureTriggered = true;
                throw new InvalidOperationException("Injected notification failure.");
            }
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<DbContextOptions<ClassScheduleDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ClassScheduleDbContext>>();
            services.AddDbContext<ClassScheduleDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Default Timeout=1").AddInterceptors(new FailureInterceptor(this)));
            using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ClassScheduleDbContext>().Database.EnsureCreated();
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
