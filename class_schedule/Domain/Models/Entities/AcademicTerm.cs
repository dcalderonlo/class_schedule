namespace class_schedule.Domain.Models.Entities;

public class AcademicTerm
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    private AcademicTerm() { }
    public AcademicTerm(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name) || startDate == default || endDate < startDate) throw new ArgumentException("Invalid term.");
        Name = name.Trim(); StartDate = startDate; EndDate = endDate;
    }
}
