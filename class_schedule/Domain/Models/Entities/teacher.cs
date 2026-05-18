namespace class_schedule.Domain.Models.Entities;

public class Teacher
{
  public int Id { get; set; }
  public string FirstName { get; set; } = string.Empty;
  public string LastName { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;

  public string FullName => $"{FirstName} {LastName}";

  public ICollection<Schedule> Schedules { get; set; } = [];
}