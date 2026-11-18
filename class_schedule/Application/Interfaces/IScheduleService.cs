using class_schedule.Application.DTOs;

namespace class_schedule.Application.Interfaces;

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduleDto>> GetAllAsync(int offset, int size, CancellationToken ct = default);
    Task<ScheduleDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ScheduleDto> CreateAsync(CreateScheduleDto dto, CancellationToken ct = default);
    Task<ScheduleDto> UpdateAsync(int id, CreateScheduleDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
