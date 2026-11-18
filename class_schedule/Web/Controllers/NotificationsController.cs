using class_schedule.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace class_schedule.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        long offset = ((long)page - 1) * pageSize;
        if (page < 1 || pageSize < 1 || pageSize > 100 || offset > int.MaxValue)
            return BadRequest(new { error = "Invalid pagination bounds." });
        return Ok(await notifications.ListAsync((int)offset, pageSize, ct));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) =>
        await notifications.ReadAsync(id, ct) ? NoContent() : NotFound();
}
