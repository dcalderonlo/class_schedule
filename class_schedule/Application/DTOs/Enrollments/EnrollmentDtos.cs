namespace class_schedule.Application.DTOs.Enrollments;

public record SelectSection(int CourseSectionId);
public record EnrollmentDto(int Id, string UserId, int CourseOfferingId, int CourseSectionId);
public record EnrollmentUserDto(string Id, string? Email);
