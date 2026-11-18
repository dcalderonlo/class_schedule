using System.ComponentModel.DataAnnotations;
using class_schedule.Domain.Models.Enums;

namespace class_schedule.Application.DTOs.Catalog;

public record SubjectDto(int Id, string Name, string Code, int WeeklyHours);
public record TeacherDto(int Id, string Name);
public record RoomDto(int Id, string Number, int Capacity, RoomType Type);
public record TermDto(int Id, string Name, DateOnly StartDate, DateOnly EndDate);
public record OfferingDto(int Id, int SubjectId, int AcademicTermId);
public record SectionDto(int Id, int CourseOfferingId, string Code, int Capacity);

public record CreateSubject([Required, StringLength(100)] string Name, [Required, StringLength(20)] string Code, [Range(1, int.MaxValue)] int WeeklyHours);
public record CreateTeacher([Required, StringLength(100)] string FirstName, [Required, StringLength(100)] string LastName, [Required, StringLength(200), EmailAddress] string Email);
public record CreateRoom([Required, StringLength(20)] string Number, [Range(1, int.MaxValue)] int Capacity, [EnumDataType(typeof(RoomType))] RoomType Type);
public record CreateTerm([Required, StringLength(100)] string Name, DateOnly StartDate, DateOnly EndDate);
public record CreateOffering([Range(1, int.MaxValue)] int SubjectId, [Range(1, int.MaxValue)] int AcademicTermId);
public record CreateSection([Range(1, int.MaxValue)] int CourseOfferingId, [Required, StringLength(20)] string Code, [Range(1, int.MaxValue)] int Capacity);
