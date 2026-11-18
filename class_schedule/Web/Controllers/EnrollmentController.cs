using class_schedule.Application.DTOs.Enrollments;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace class_schedule.Web.Controllers;

public sealed class EnrollmentErrorFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not EnrollmentException error) return;
        context.Result = new ObjectResult(new { error = error.Message }) { StatusCode = error.StatusCode };
        context.ExceptionHandled = true;
    }
}

[ApiController]
[Authorize]
[EnrollmentErrorFilter]
[Route("api/enrollments")]
public sealed class EnrollmentController(IEnrollmentService service) : ControllerBase
{
    private static bool Paging(int page, int size, out int offset)
    {
        long value = ((long)page - 1) * size;
        offset = value is >= 0 and <= int.MaxValue ? (int)value : 0;
        return page >= 1 && size is >= 1 and <= 100 && value <= int.MaxValue;
    }
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Paging(page, pageSize, out int offset) ? Ok(await service.ListAsync(null, offset, pageSize, ct)) : BadRequest(new { error = "Invalid pagination bounds." });
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct); return result is null ? NotFound() : Ok(result);
    }
    [HttpGet("timetable")]
    public async Task<IActionResult> Timetable(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Paging(page, pageSize, out int offset) ? Ok(await service.TimetableAsync(offset, pageSize, ct)) : BadRequest(new { error = "Invalid pagination bounds." });
    [HttpGet("users"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Users(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Paging(page, pageSize, out int offset) ? Ok(await service.UsersAsync(offset, pageSize, ct)) : BadRequest(new { error = "Invalid pagination bounds." });
    [HttpGet("users/{userId}"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> UserEnrollments(string userId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Paging(page, pageSize, out int offset) ? Ok(await service.ListAsync(userId, offset, pageSize, ct)) : BadRequest(new { error = "Invalid pagination bounds." });
    [HttpPost]
    public async Task<IActionResult> Create(SelectSection input, CancellationToken ct)
    {
        var result = await service.CreateAsync(null, input.CourseSectionId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPost("users/{userId}"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Assign(string userId, SelectSection input, CancellationToken ct)
    {
        var result = await service.CreateAsync(userId, input.CourseSectionId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Switch(int id, SelectSection input, CancellationToken ct) => Ok(await service.SwitchAsync(id, input.CourseSectionId, ct));
}
