namespace class_schedule.Domain.Models.Entities;

public class CourseSection
{
    public int Id { get; private set; }
    public int CourseOfferingId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    private CourseSection() { }
    public CourseSection(int courseOfferingId, string code, int capacity)
    {
        if (courseOfferingId <= 0 || string.IsNullOrWhiteSpace(code) || capacity <= 0) throw new ArgumentException("Invalid section.");
        CourseOfferingId = courseOfferingId; Code = code.Trim().ToUpperInvariant(); Capacity = capacity;
    }
}
