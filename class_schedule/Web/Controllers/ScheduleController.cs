using class_schedule.Application.DTOs;
using class_schedule.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace class_schedule.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController(IScheduleService scheduleService) : ControllerBase
{
  private readonly IScheduleService scheduleService = scheduleService;

  // GET: api/schedule
  [HttpGet]
  public async Task<ActionResult<IEnumerable<ScheduleDto>>> GetSchedule(
    CancellationToken ct)
  {
    var schedules = await scheduleService.GetAllAsync(ct);
    return Ok(schedules);
  }

  // GET: api/schedule/5
  [HttpGet("{id:int}")]
  public async Task<ActionResult<ScheduleDto>> GetScheduleById(
    int id, CancellationToken ct)
  {
    var schedule = await scheduleService.GetByIdAsync(id, ct);
    return schedule is null ? NotFound() : Ok(schedule);
  }

  // POST: api/schedule
  [HttpPost]
  public async Task<ActionResult<ScheduleDto>> CreateSchedule(
    CreateScheduleDto dto, CancellationToken ct)
  {
    try
    {
      var created = await scheduleService.CreateAsync(dto, ct);
      return CreatedAtAction(
        nameof(GetScheduleById),
        new { id = created.Id },
        created);
    }
    catch (InvalidOperationException ex)
    {
      // Conflicto de horario → 409 Conflict
      return Conflict(new { error = ex.Message });
    }
  }

  // PUT: api/schedule/5
  [HttpPut("{id:int}")]
  public async Task<ActionResult<ScheduleDto>> UpdateSchedule(
    int id, CreateScheduleDto dto, CancellationToken ct)
  {
    try
    {
      var updated = await scheduleService.UpdateAsync(id, dto, ct);
      return Ok(updated);
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
    catch (InvalidOperationException ex)
    {
      return Conflict(new { error = ex.Message });
    }
  }

  // DELETE: api/schedule/5
  [HttpDelete("{id:int}")]
  public async Task<IActionResult> DeleteSchedule(int id, CancellationToken ct)
  {
    try
    {
      await scheduleService.DeleteAsync(id, ct);
      return NoContent();  // 204 — eliminado correctamente
    }
    catch (KeyNotFoundException)
    {
      return NotFound();
    }
  }
}