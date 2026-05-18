using class_schedule.Domain.Models.Enums;

namespace class_schedule.Domain.Models.Entities;

public class Schedule
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime   { get; set; }

    // Asignatura
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    
    // Profesor
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    // Aula
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public GradeLevel GradeLevel { get; set; }
}