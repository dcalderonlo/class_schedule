using System.Security.Claims;
using class_schedule.Application.Security;

namespace class_schedule.Infrastructure.Identity;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsAdministrator => accessor.HttpContext?.User.IsInRole(SecurityPolicies.Administrator) == true;
}
