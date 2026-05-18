using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
  public void Configure(EntityTypeBuilder<Subject> builder)
  {
    // ── Clave primaria ──────────────────────────────────────────────
    builder.HasKey(s => s.Id);

    // ── Propiedades ─────────────────────────────────────────────────

    // Nombre de la materia. Texto libre pero acotado.
    // Ej: "Matemáticas", "Lengua Española", "Ciencias Naturales"
    builder.Property(s => s.Name)
          .HasMaxLength(100)
          .IsRequired();

    // Código único de la materia.
    // Ej: "MAT-401", "ESP-301"
    builder.Property(s => s.Code)
          .HasMaxLength(20)
          .IsRequired();

    // Horas por semana que ocupa la materia.
    // Usado para validar que el horario no exceda el límite semanal.
    builder.Property(s => s.WeeklyHours)
          .IsRequired();

    // ── Índices ─────────────────────────────────────────────────────

    // El código de materia debe ser único en todo el sistema.
    // Evita duplicados como dos "MAT-401" con nombres distintos.
    builder.HasIndex(s => s.Code)
          .IsUnique()
          .HasDatabaseName("IX_Subject_Code");
  }
}