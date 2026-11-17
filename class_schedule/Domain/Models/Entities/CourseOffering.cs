namespace class_schedule.Domain.Models.Entities;

public class CourseOffering
{
    public int Id { get; private set; }
    public int SubjectId { get; private set; }
    public int AcademicTermId { get; private set; }
    private CourseOffering() { }
    public CourseOffering(int subjectId, int academicTermId)
    {
        if (subjectId <= 0 || academicTermId <= 0) throw new ArgumentException("Invalid offering.");
        SubjectId = subjectId; AcademicTermId = academicTermId;
    }
}
