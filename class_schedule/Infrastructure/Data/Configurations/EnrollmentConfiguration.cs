using class_schedule.Domain.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.CourseSectionId, x.CourseOfferingId })
            .HasPrincipalKey(x => new { x.Id, x.CourseOfferingId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.CourseOfferingId }).IsUnique();
        builder.HasIndex(x => x.CourseSectionId);
    }
}
