using class_schedule.Application.DTOs;
using class_schedule.Application.DTOs.Enrollments;

namespace class_schedule.Application.Interfaces;

public interface IEnrollmentService
{
    Task<IReadOnlyList<EnrollmentDto>> ListAsync(string? userId, int offset, int size, CancellationToken ct);
    Task<EnrollmentDto?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentUserDto>> UsersAsync(int offset, int size, CancellationToken ct);
    Task<IReadOnlyList<ScheduleDto>> TimetableAsync(int offset, int size, CancellationToken ct);
    Task<EnrollmentDto> CreateAsync(string? userId, int sectionId, CancellationToken ct);
    Task<EnrollmentDto> SwitchAsync(int id, int sectionId, CancellationToken ct);
}
