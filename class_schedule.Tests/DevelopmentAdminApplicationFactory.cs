using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace class_schedule.Tests;

public sealed class DevelopmentAdminApplicationFactory(SqliteConnection connection, string environment, Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.RemoveAll<DbContextOptions<ClassScheduleDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ClassScheduleDbContext>>();
            services.AddDbContext<ClassScheduleDbContext>(options => options.UseSqlite(connection));
            configure?.Invoke(services);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ClassScheduleDbContext>().Database.EnsureCreated();
        });
    }
}
