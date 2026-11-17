namespace class_schedule.Domain.Models.Entities;

public class Schedule
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
