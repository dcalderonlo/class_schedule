namespace class_schedule.Domain.Models.Entities;

public class Subject
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Code { get; set; } = string.Empty;
  public int WeeklyHours { get; set; }

  public ICollection<Schedule> Schedules { get; set; } = [];
}