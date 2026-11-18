using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
{
    public void Configure(EntityTypeBuilder<AcademicTerm> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_AcademicTerm_Dates", "StartDate <= EndDate"));
    }
}
