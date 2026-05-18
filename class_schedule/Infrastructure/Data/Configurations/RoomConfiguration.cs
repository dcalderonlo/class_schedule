using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
  public void Configure(EntityTypeBuilder<Room> builder)
  {
    // ── Clave primaria ──────────────────────────────────────────────
    builder.HasKey(r => r.Id);

    // ── Propiedades ─────────────────────────────────────────────────

    // Código o número del aula.
    // Acepta formatos alfanuméricos: "A-101", "Lab-2", "Gym"
    builder.Property(r => r.Number)
          .HasMaxLength(20)
          .IsRequired();

    // Capacidad máxima de estudiantes del aula.
    // Útil para validar que el grado no supere el aforo.
    builder.Property(r => r.Capacity)
          .IsRequired();

    // RoomType es un enum propio (Regular, Laboratory, Gymnasium…).
    // Se guarda como string para legibilidad en BD.
    builder.Property(r => r.Type)
          .HasConversion<string>()
          .HasMaxLength(20)
          .IsRequired();

    // ── Índices ─────────────────────────────────────────────────────

    // El número de aula debe ser único en el centro.
    // Evita registrar "A-101" dos veces con distinta capacidad.
    builder.HasIndex(r => r.Number)
          .IsUnique()
          .HasDatabaseName("IX_Room_Number");
  }
}