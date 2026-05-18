using class_schedule.Domain.Models.Enums;

namespace class_schedule.Domain.Models.Entities;

public class Room
{
  public int Id { get; set; }
  public string Number { get; set; } = string.Empty;
  public int Capacity { get; set; }
  public RoomType Type { get; set; }

  public ICollection<Schedule> Schedules { get; set; } = [];
}