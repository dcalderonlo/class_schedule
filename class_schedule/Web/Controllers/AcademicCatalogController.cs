using class_schedule.Application.DTOs.Catalog;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace class_schedule.Web.Controllers;

public sealed class CatalogErrorFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not CatalogException error) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = error.StatusCode, Title = error.Message })
        { StatusCode = error.StatusCode, ContentTypes = { "application/problem+json" } };
        context.ExceptionHandled = true;
    }
}

[ApiController]
[Authorize]
[CatalogErrorFilter]
[Route("api/catalog")]
public class AcademicCatalogController(IAcademicCatalogService catalog) : ControllerBase
{
    [HttpGet("{resource}")]
    public async Task<IActionResult> List(string resource, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        long offset = ((long)page - 1) * pageSize;
        if (page < 1 || pageSize < 1 || pageSize > 100 || offset > int.MaxValue)
            return Problem(statusCode: 400, title: "Invalid pagination bounds.");
        return Ok(await catalog.List(resource, (int)offset, pageSize, ct));
    }

    [HttpPost("subjects"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Subject(CreateSubject input, CancellationToken ct) => Created("/api/catalog/subjects", await catalog.Create(input, ct));
    [HttpPost("teachers"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Teacher(CreateTeacher input, CancellationToken ct) => Created("/api/catalog/teachers", await catalog.Create(input, ct));
    [HttpPost("rooms"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Room(CreateRoom input, CancellationToken ct) => Created("/api/catalog/rooms", await catalog.Create(input, ct));
    [HttpPost("terms"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Term(CreateTerm input, CancellationToken ct) => Created("/api/catalog/terms", await catalog.Create(input, ct));
    [HttpPost("offerings"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Offering(CreateOffering input, CancellationToken ct) => Created("/api/catalog/offerings", await catalog.Create(input, ct));
    [HttpPost("sections"), Authorize(Policy = SecurityPolicies.Administrator)]
    public async Task<IActionResult> Section(CreateSection input, CancellationToken ct) => Created("/api/catalog/sections", await catalog.Create(input, ct));
}
