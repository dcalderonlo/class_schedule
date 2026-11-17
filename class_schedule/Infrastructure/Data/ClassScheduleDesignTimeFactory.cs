using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace class_schedule.Infrastructure.Data;

public sealed class ClassScheduleDesignTimeFactory : IDesignTimeDbContextFactory<ClassScheduleDbContext>
{
    public ClassScheduleDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ClassSchedule");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__ClassSchedule before using EF tools.");

        return new ClassScheduleDbContext(
            new DbContextOptionsBuilder<ClassScheduleDbContext>().UseSqlServer(connection).Options);
    }
}
