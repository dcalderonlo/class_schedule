using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class CourseSectionConfiguration : IEntityTypeConfiguration<CourseSection>
{
    public void Configure(EntityTypeBuilder<CourseSection> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CourseOfferingId });
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CourseOfferingId, x.Code }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_CourseSection_Positive", "CourseOfferingId > 0 AND Capacity > 0"));
    }
}
