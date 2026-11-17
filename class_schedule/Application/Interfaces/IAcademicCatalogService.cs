using class_schedule.Application.DTOs.Catalog;

namespace class_schedule.Application.Interfaces;

public interface IAcademicCatalogService
{
    Task<object> List(string resource, int offset, int size, CancellationToken ct);
    Task<SubjectDto> Create(CreateSubject input, CancellationToken ct);
    Task<TeacherDto> Create(CreateTeacher input, CancellationToken ct);
    Task<RoomDto> Create(CreateRoom input, CancellationToken ct);
    Task<TermDto> Create(CreateTerm input, CancellationToken ct);
    Task<OfferingDto> Create(CreateOffering input, CancellationToken ct);
    Task<SectionDto> Create(CreateSection input, CancellationToken ct);
}
