namespace class_schedule.Models.Entities;

public class Subject
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Code { get; set; } = string.Empty;
  public int WeeklyHours { get; set; }

  public ICollection<Schedule> Schedule { get; set; } = [];
}