using class_schedule.Domain.Models.Enums;

namespace class_schedule.Application.DTOs;

// Lo que el API recibe en POST y PUT — solo IDs de relaciones, sin Id propio
public record CreateScheduleDto(
  int        SubjectId,
  int        TeacherId,
  int        RoomId,
  DayOfWeek  DayOfWeek,
  TimeOnly   StartTime,
  TimeOnly   EndTime,
  GradeLevel GradeLevel
);