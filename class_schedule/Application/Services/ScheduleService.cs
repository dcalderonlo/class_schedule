// using class_schedule.Application.Interfaces;
// using class_schedule.Domain.Models.Entities;
// using class_schedule.Infrastructure.Data;
// using Microsoft.EntityFrameworkCore;

// namespace class_schedule.Application.Services;

// public class ScheduleService(ClassScheduleDbContext db) : IScheduleService
// {
//   // ── GET ALL ────────────────────────────────────────────────────────
//   public async Task<IEnumerable<Schedule>> GetAllAsync()
//     => await db.Schedules
//       .Include(s => s.Subject)
//       .Include(s => s.Teacher)
//       .Include(s => s.Room)
//       .AsNoTracking()
//       .ToListAsync();

//   // ── GET BY ID ──────────────────────────────────────────────────────
//   public async Task<Schedule?> GetByIdAsync(int id)
//     => await db.Schedules
//       .Include(s => s.Subject)
//       .Include(s => s.Teacher)
//       .Include(s => s.Room)
//       .AsNoTracking()
//       .FirstOrDefaultAsync(s => s.Id == id);

//   // ── CREATE ─────────────────────────────────────────────────────────
//   public async Task<Schedule> CreateAsync(Schedule item)
//   {
//     await ValidateConflictsAsync(item);

//     db.Schedules.Add(item);
//     await db.SaveChangesAsync();
//     return item;
//   }

//   // ── UPDATE ─────────────────────────────────────────────────────────
//   public async Task<Schedule> UpdateAsync(int id, Schedule item)
//   {
//     ArgumentNullException.ThrowIfNull(item);

//     var existing = await db.Schedules.FindAsync(id)
//       ?? throw new KeyNotFoundException($"Schedule {id} no encontrado.");

//     // Garantiza que el Id del objeto entrante no sobreescriba la clave primaria
//     item.Id = id;
//     db.Entry(existing).CurrentValues.SetValues(item);

//     await ValidateConflictsAsync(existing, excludeId: id);
//     await db.SaveChangesAsync();
//     return existing;
//   }

//   // ── DELETE ─────────────────────────────────────────────────────────
//   public async Task DeleteAsync(int id)
//   {
//     var item = await db.Schedules.FindAsync(id)
//       ?? throw new KeyNotFoundException($"Schedule {id} no encontrado.");

//     db.Schedules.Remove(item);
//     await db.SaveChangesAsync();
//   }

//   // ── VALIDACIÓN DE CONFLICTOS ───────────────────────────────────────
//   // Centralizada aquí para que tanto Create como Update la reutilicen.
//   // excludeId permite ignorar el propio registro al validar un Update.
//   private async Task ValidateConflictsAsync(Schedule item, int? excludeId = null)
//   {
//     // Candidatos: mismo día, con solapamiento de horario
//     var overlapping = await db.Schedules
//       .Where(s => s.Id != excludeId
//           && s.DayOfWeek  == item.DayOfWeek
//           && s.StartTime  <  item.EndTime
//           && s.EndTime    >  item.StartTime)
//       .ToListAsync();

//     // Conflicto 1: misma aula, mismo horario
//     var roomConflict = overlapping.FirstOrDefault(s => s.RoomId == item.RoomId);
//     if (roomConflict is not null)
//       throw new InvalidOperationException(
//         $"El aula ya está ocupada el {item.DayOfWeek} " +
//         $"de {roomConflict.StartTime} a {roomConflict.EndTime}.");

//     // Conflicto 2: mismo profesor, mismo horario
//     var teacherConflict = overlapping.FirstOrDefault(s => s.TeacherId == item.TeacherId);
//     if (teacherConflict is not null)
//       throw new InvalidOperationException(
//         $"El profesor ya tiene clase asignada el {item.DayOfWeek} " +
//         $"de {teacherConflict.StartTime} a {teacherConflict.EndTime}.");

//     // Conflicto 3: mismo grado, mismo horario
//     var gradeConflict = overlapping.FirstOrDefault(s => s.GradeLevel == item.GradeLevel);
//     if (gradeConflict is not null)
//       throw new InvalidOperationException(
//         $"El grado {item.GradeLevel} ya tiene clase asignada el {item.DayOfWeek} " +
//         $"de {gradeConflict.StartTime} a {gradeConflict.EndTime}.");
//   }
// }


using class_schedule.Application.DTOs;
using class_schedule.Application.Interfaces;
using class_schedule.Domain.Models.Entities;
using class_schedule.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Application.Services;

public class ScheduleService(ClassScheduleDbContext db) : IScheduleService
{
  // ── QUERY BASE ────────────────────────────────────────────────────
  private IQueryable<Schedule> QueryWithIncludes() =>
    db.Schedules
      .Include(s => s.Subject)
      .Include(s => s.Teacher)
      .Include(s => s.Room);

  // ── GET ALL ───────────────────────────────────────────────────────
  public async Task<IEnumerable<ScheduleDto>> GetAllAsync(CancellationToken ct = default)
  {
    var schedules = await QueryWithIncludes()
      .AsNoTracking()
      .ToListAsync(ct);

    return schedules.Select(ToDto);
  }

  // ── GET BY ID ─────────────────────────────────────────────────────
  public async Task<ScheduleDto?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    var schedule = await QueryWithIncludes()
      .AsNoTracking()
      .FirstOrDefaultAsync(s => s.Id == id, ct);

    return schedule is null ? null : ToDto(schedule);
  }

  // ── CREATE ────────────────────────────────────────────────────────
  public async Task<ScheduleDto> CreateAsync(CreateScheduleDto dto, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(dto);

    var schedule = ToEntity(dto);

    await ValidateConflictsAsync(schedule, ct);

    db.Schedules.Add(schedule);
    await db.SaveChangesAsync(ct);

    var created = await QueryWithIncludes()
      .FirstAsync(s => s.Id == schedule.Id, ct);

    return ToDto(created);
  }

  // ── UPDATE ────────────────────────────────────────────────────────
  public async Task<ScheduleDto> UpdateAsync(int id, CreateScheduleDto dto, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(dto);

    var existing = await db.Schedules.FindAsync([id], ct)
      ?? throw new KeyNotFoundException($"Schedule {id} no encontrado.");

    // Asigna el Id antes de SetValues para proteger la clave primaria
    var entity = ToEntity(dto);
    entity.Id  = id;
    db.Entry(existing).CurrentValues.SetValues(entity);

    await ValidateConflictsAsync(existing, ct, excludeId: id);
    await db.SaveChangesAsync(ct);

    var updated = await QueryWithIncludes()
      .FirstAsync(s => s.Id == id, ct);

    return ToDto(updated);
  }

  // ── DELETE ────────────────────────────────────────────────────────
  public async Task DeleteAsync(int id, CancellationToken ct = default)
  {
    var schedule = await db.Schedules.FindAsync([id], ct)
      ?? throw new KeyNotFoundException($"Schedule {id} no encontrado.");

    db.Schedules.Remove(schedule);
    await db.SaveChangesAsync(ct);
  }

  // ── VALIDACIÓN DE CONFLICTOS ──────────────────────────────────────
  private async Task ValidateConflictsAsync(
    Schedule schedule, CancellationToken ct, int? excludeId = null)
  {
    bool roomConflict = await db.Schedules.AnyAsync(s =>
      s.Id        != excludeId      &&
      s.DayOfWeek == schedule.DayOfWeek &&
      s.StartTime <  schedule.EndTime &&
      s.EndTime   >  schedule.StartTime &&
      s.RoomId    == schedule.RoomId, ct);

    if (roomConflict)
      throw new InvalidOperationException(
        $"El aula ya está ocupada el {schedule.DayOfWeek} " +
        $"de {schedule.StartTime} a {schedule.EndTime}.");

    bool teacherConflict = await db.Schedules.AnyAsync(s =>
      s.Id        != excludeId      &&
      s.DayOfWeek == schedule.DayOfWeek &&
      s.StartTime <  schedule.EndTime &&
      s.EndTime   >  schedule.StartTime &&
      s.TeacherId == schedule.TeacherId, ct);

    if (teacherConflict)
      throw new InvalidOperationException(
        $"El profesor ya tiene clase asignada el {schedule.DayOfWeek} " +
        $"de {schedule.StartTime} a {schedule.EndTime}.");

    bool gradeConflict = await db.Schedules.AnyAsync(s =>
      s.Id        != excludeId         &&
      s.DayOfWeek == schedule.DayOfWeek &&
      s.StartTime <  schedule.EndTime  &&
      s.EndTime   >  schedule.StartTime &&
      s.GradeLevel == schedule.GradeLevel, ct);

    if (gradeConflict)
      throw new InvalidOperationException(
        $"El grado {schedule.GradeLevel} ya tiene clase asignada el {schedule.DayOfWeek} " +
        $"de {schedule.StartTime} a {schedule.EndTime}.");
  }

    // ── MAPEOS ────────────────────────────────────────────────────────
  private static ScheduleDto ToDto(Schedule s) => new(
    s.Id,
    s.Subject.Name,
    s.Teacher.FullName,
    s.Room.Number,
    s.DayOfWeek,
    s.StartTime,
    s.EndTime,
    s.GradeLevel
  );

  private static Schedule ToEntity(CreateScheduleDto dto) => new()
  {
    SubjectId  = dto.SubjectId,
    TeacherId  = dto.TeacherId,
    RoomId     = dto.RoomId,
    DayOfWeek  = dto.DayOfWeek,   // ← corregido
    StartTime  = dto.StartTime,
    EndTime    = dto.EndTime,
    GradeLevel = dto.GradeLevel
  };
}