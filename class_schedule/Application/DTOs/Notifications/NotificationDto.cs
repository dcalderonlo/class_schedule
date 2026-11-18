namespace class_schedule.Application.DTOs.Notifications;

public record NotificationDto(int Id, string Message, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
