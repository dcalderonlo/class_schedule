using class_schedule.Models.Enums;

namespace class_schedule.Models.Entities;

public class Room
{
  public int Id { get; set; }
  public string Number { get; set; } = string.Empty;
  public int Capacity { get; set; }
  public RoomType Type { get; set; }

  public ICollection<Schedule> Schedule { get; set; } = [];
}