namespace class_schedule.Domain.Models.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CourseOfferingId { get; set; }
    public int CourseSectionId { get; set; }
}
