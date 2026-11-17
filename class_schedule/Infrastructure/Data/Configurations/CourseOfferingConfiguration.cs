using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOffering>
{
    public void Configure(EntityTypeBuilder<CourseOffering> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => x.AcademicTermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SubjectId, x.AcademicTermId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_CourseOffering_Ids", "SubjectId > 0 AND AcademicTermId > 0"));
    }
}
