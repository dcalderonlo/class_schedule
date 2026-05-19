using class_schedule.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace class_schedule.Infrastructure.Data.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
  public void Configure(EntityTypeBuilder<Schedule> builder)
  {
    // ── Clave primaria ──────────────────────────────────────────────
    builder.HasKey(s => s.Id);

    // ── Propiedades ─────────────────────────────────────────────────

    // DayOfWeek es un enum de .NET (System.DayOfWeek).
    // Se persiste como string legible en BD ("Monday", "Tuesday"…)
    // en lugar del entero por defecto, para facilitar consultas directas.
    builder.Property(s => s.DayOfWeek)
          .HasConversion<string>()
          .HasMaxLength(10)
          .IsRequired();

    // TimeOnly mapea al tipo SQL "time" (sin fecha).
    // EF Core no lo infiere solo, hay que declararlo explícitamente.
    builder.Property(s => s.StartTime)
          .HasColumnType("time")
          .IsRequired();

    builder.Property(s => s.EndTime)
          .HasColumnType("time")
          .IsRequired();

    // GradeLevel es un enum propio.
    // Se guarda como string ("FourthYearHighSchool") para legibilidad en BD.
    builder.Property(s => s.GradeLevel)
          .HasConversion<string>()
          .HasMaxLength(30)
          .IsRequired();

    // ── Relaciones ──────────────────────────────────────────────────

    // Un Schedule tiene un Subject.
    // Restrict: no se puede eliminar un Subject si tiene clases asignadas.
    builder.HasOne(s => s.Subject)
          .WithMany(sub => sub.Schedules)
          .HasForeignKey(s => s.SubjectId)
          .OnDelete(DeleteBehavior.Restrict);

    // Un Schedule tiene un Teacher.
    // Restrict: no se puede eliminar un Teacher con clases asignadas.
    builder.HasOne(s => s.Teacher)
          .WithMany(t => t.Schedules)
          .HasForeignKey(s => s.TeacherId)
          .OnDelete(DeleteBehavior.Restrict);

    // Un Schedule tiene un Room.
    // Restrict: no se puede eliminar un Room con clases asignadas.
    builder.HasOne(s => s.Room)
          .WithMany(r => r.Schedules)
          .HasForeignKey(s => s.RoomId)
          .OnDelete(DeleteBehavior.Restrict);

    // ── Índices ─────────────────────────────────────────────────────

    // Optimiza la consulta de conflictos de aula
    builder.HasIndex(s => new { s.DayOfWeek, s.StartTime, s.RoomId })
          .HasDatabaseName("IX_Schedule_Room_Time");

    // Optimiza la consulta de conflictos de profesor
    builder.HasIndex(s => new { s.DayOfWeek, s.StartTime, s.TeacherId })
          .HasDatabaseName("IX_Schedule_Teacher_Time");

    // Optimiza la consulta de conflictos de grado
    builder.HasIndex(s => new { s.DayOfWeek, s.StartTime, s.GradeLevel })
          .HasDatabaseName("IX_Schedule_Grade_Time");
  }
}