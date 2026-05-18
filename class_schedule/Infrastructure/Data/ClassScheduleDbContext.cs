using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Infrastructure.Data;

public class ClassScheduleDbContext(DbContextOptions<ClassScheduleDbContext> options)
  : DbContext(options)
{
  public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(ClassScheduleDbContext).Assembly);
    }
}