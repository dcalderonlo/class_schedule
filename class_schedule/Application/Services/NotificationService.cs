using class_schedule.Application.DTOs.Notifications;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using class_schedule.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Application.Services;

public sealed class NotificationService(ClassScheduleDbContext db, ICurrentUser actor) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(int offset, int size, CancellationToken ct)
    {
        var userId = actor.UserId;
        if (userId is null) return Array.Empty<NotificationDto>();
        return await db.UserNotifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id).Skip(offset).Take(size)
            .Select(x => new NotificationDto(x.Id, x.Message, x.CreatedAtUtc, x.ReadAtUtc)).ToListAsync(ct);
    }

    public async Task<bool> ReadAsync(int id, CancellationToken ct)
    {
        var userId = actor.UserId;
        if (userId is null) return false;
        var notification = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (notification is null) return false;
        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }
}
