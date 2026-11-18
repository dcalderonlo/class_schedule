using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace class_schedule.Infrastructure.Data;

public class ClassScheduleDbContext(DbContextOptions<ClassScheduleDbContext> options)
  : IdentityDbContext<IdentityUser>(options)
{
  public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);
      modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(ClassScheduleDbContext).Assembly);
    }
}