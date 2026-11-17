using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DayOfWeek).HasConversion<int>();
        builder.Property(s => s.StartTime).HasColumnType("time").IsRequired();
        builder.Property(s => s.EndTime).HasColumnType("time").IsRequired();
        builder.HasOne(s => s.CourseSection).WithMany().HasForeignKey(s => s.CourseSectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Teacher).WithMany(t => t.Schedules).HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Room).WithMany(r => r.Schedules).HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.RoomId, s.DayOfWeek, s.StartTime });
        builder.HasIndex(s => new { s.TeacherId, s.DayOfWeek, s.StartTime });
        builder.HasIndex(s => new { s.CourseSectionId, s.DayOfWeek, s.StartTime });
        builder.ToTable(t => t.HasCheckConstraint("CK_Schedule_Valid", "CourseSectionId > 0 AND TeacherId > 0 AND RoomId > 0 AND DayOfWeek >= 0 AND DayOfWeek <= 6 AND StartTime < EndTime"));
    }
}
