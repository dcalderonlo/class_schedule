using class_schedule.Application.DTOs;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace class_schedule.Web.Controllers;

public sealed class SchedulingErrorFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not SchedulingException error) return;
        context.Result = new ObjectResult(new { error = error.Message }) { StatusCode = error.StatusCode };
        context.ExceptionHandled = true;
    }
}

[ApiController]
[Authorize]
[SchedulingErrorFilter]
[Route("api/[controller]")]
public class ScheduleController(IScheduleService scheduleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScheduleDto>>> GetSchedule([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        long offset = ((long)page - 1) * pageSize;
        if (page < 1 || pageSize < 1 || pageSize > 100 || offset > int.MaxValue)
            return BadRequest(new { error = "Invalid pagination bounds." });
        return Ok(await scheduleService.GetAllAsync((int)offset, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ScheduleDto>> GetScheduleById(int id, CancellationToken ct)
    {
        var schedule = await scheduleService.GetByIdAsync(id, ct);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPost, Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(CreateScheduleDto dto, CancellationToken ct)
    {
        var created = await scheduleService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetScheduleById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<ActionResult<ScheduleDto>> UpdateSchedule(int id, CreateScheduleDto dto, CancellationToken ct) =>
        Ok(await scheduleService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> DeleteSchedule(int id, CancellationToken ct)
    {
        await scheduleService.DeleteAsync(id, ct);
        return NoContent();
    }
}
