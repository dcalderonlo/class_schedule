using System.ComponentModel.DataAnnotations;
using System.Data;
using class_schedule.Application.Security;
using class_schedule.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Infrastructure.Identity;

public sealed class DevelopmentAdminInitializer(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = configuration.GetSection("DevelopmentAdmin").Get<DevelopmentAdminOptions>() ?? new();
        if (!options.Enabled) return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Development administrator bootstrap is permitted only in Development.");
        if (string.IsNullOrWhiteSpace(options.Email) || options.Email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(options.Email) || string.IsNullOrEmpty(options.Password) || options.Password.Length > 128)
            throw new InvalidOperationException("Development administrator configuration is invalid; check the email and password requirements.");

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClassScheduleDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var candidate = new IdentityUser { UserName = options.Email, Email = options.Email };
            foreach (var validator in users.PasswordValidators)
                if (!(await validator.ValidateAsync(users, candidate, options.Password)).Succeeded)
                    throw new InvalidOperationException();

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await users.FindByEmailAsync(options.Email);
            if (existing is not null && await users.IsInRoleAsync(existing, SecurityPolicies.Administrator)) return;
            if (await users.Users.AnyAsync(cancellationToken)) throw new InvalidOperationException();
            if (!await roles.RoleExistsAsync(SecurityPolicies.Administrator) &&
                !(await roles.CreateAsync(new IdentityRole(SecurityPolicies.Administrator))).Succeeded)
                throw new InvalidOperationException();
            if (!(await users.CreateAsync(candidate, options.Password)).Succeeded ||
                !(await users.AddToRoleAsync(candidate, SecurityPolicies.Administrator)).Succeeded)
                throw new InvalidOperationException();
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Development administrator bootstrap failed. Check configuration, the empty user store and the reviewed database schema.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
