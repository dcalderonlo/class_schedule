using class_schedule.Application.DTOs.Notifications;

namespace class_schedule.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(int offset, int size, CancellationToken ct);
    Task<bool> ReadAsync(int id, CancellationToken ct);
}
