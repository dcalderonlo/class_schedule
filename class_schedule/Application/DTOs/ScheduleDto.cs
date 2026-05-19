using class_schedule.Domain.Models.Enums;

namespace class_schedule.Application.DTOs;

// Lo que el API devuelve — incluye datos resueltos de las relaciones, y el Id del Schedule
public record ScheduleDto(
  int       Id,
  string    SubjectName,
  string    TeacherFullName,
  string    RoomNumber,
  DayOfWeek DayOfWeek,
  TimeOnly  StartTime,
  TimeOnly  EndTime,
  GradeLevel GradeLevel
);