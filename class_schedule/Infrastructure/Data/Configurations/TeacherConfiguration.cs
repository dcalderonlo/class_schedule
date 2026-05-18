using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
  public void Configure(EntityTypeBuilder<Teacher> builder)
  {
    // ── Clave primaria ──────────────────────────────────────────────
    builder.HasKey(t => t.Id);

    // ── Propiedades ─────────────────────────────────────────────────

    // Nombre y apellido separados para facilitar ordenamiento
    // y búsquedas por apellido en el sistema.
    builder.Property(t => t.FirstName)
          .HasMaxLength(100)
          .IsRequired();

    builder.Property(t => t.LastName)
          .HasMaxLength(100)
          .IsRequired();

    // Email institucional del profesor.
    // Se usa como identificador único además del Id.
    builder.Property(t => t.Email)
          .HasMaxLength(200)
          .IsRequired();

    // FullName es una propiedad calculada (FirstName + LastName).
    // No existe como columna en la BD, EF Core debe ignorarla
    // para evitar un error al generar la migración.
    builder.Ignore(t => t.FullName);

    // ── Índices ─────────────────────────────────────────────────────

    // El email debe ser único: un profesor no puede registrarse
    // dos veces con el mismo correo institucional.
    builder.HasIndex(t => t.Email)
          .IsUnique()
          .HasDatabaseName("IX_Teacher_Email");
  }
}