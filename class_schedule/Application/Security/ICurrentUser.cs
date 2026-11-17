namespace class_schedule.Application.Security;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsAdministrator { get; }
}
