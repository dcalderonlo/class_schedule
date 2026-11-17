namespace class_schedule.Application.DTOs;

public record CreateScheduleDto(int CourseSectionId, int TeacherId, int RoomId,
    DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
