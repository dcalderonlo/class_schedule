namespace class_schedule.Application.DTOs;

public record ScheduleDto(int Id, int CourseSectionId, string SectionCode, int CourseOfferingId,
    int SubjectId, string SubjectName, int TeacherId, string TeacherFullName, int RoomId,
    string RoomNumber, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
