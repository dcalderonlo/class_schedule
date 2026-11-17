using System.Data;
using class_schedule.Application.DTOs;
using class_schedule.Application.DTOs.Enrollments;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using class_schedule.Domain.Models.Entities;
using class_schedule.Domain.Scheduling;
using class_schedule.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Application.Services;

public sealed class EnrollmentService(ClassScheduleDbContext db, ICurrentUser actor) : IEnrollmentService
{
    private static EnrollmentException Missing() => new(404, "Enrollment or referenced record not found.");
    private static EnrollmentException Conflict(string message) => new(409, message);
    private string ActorId() => actor.UserId ?? throw new EnrollmentException(401, "Authentication required.");
    private void RequireAdmin() { ActorId(); if (!actor.IsAdministrator) throw new EnrollmentException(403, "Administrator access required."); }
    private static EnrollmentDto Dto(Enrollment e) => new(e.Id, e.UserId, e.CourseOfferingId, e.CourseSectionId);

    public async Task<IReadOnlyList<EnrollmentDto>> ListAsync(string? userId, int offset, int size, CancellationToken ct)
    {
        var own = ActorId();
        if (userId is not null) RequireAdmin();
        var id = userId ?? own;
        return await db.Enrollments.AsNoTracking().Where(x => x.UserId == id).OrderBy(x => x.Id).Skip(offset).Take(size)
            .Select(x => new EnrollmentDto(x.Id, x.UserId, x.CourseOfferingId, x.CourseSectionId)).ToListAsync(ct);
    }
    public async Task<EnrollmentDto?> GetAsync(int id, CancellationToken ct)
    {
        var user = ActorId();
        return await db.Enrollments.AsNoTracking().Where(x => x.Id == id && (actor.IsAdministrator || x.UserId == user))
            .Select(x => new EnrollmentDto(x.Id, x.UserId, x.CourseOfferingId, x.CourseSectionId)).SingleOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<EnrollmentUserDto>> UsersAsync(int offset, int size, CancellationToken ct)
    {
        RequireAdmin();
        return await db.Users.AsNoTracking().OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new EnrollmentUserDto(x.Id, x.Email)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<ScheduleDto>> TimetableAsync(int offset, int size, CancellationToken ct)
    {
        var user = ActorId();
        var query = from e in db.Enrollments.AsNoTracking()
            join schedule in db.Schedules.AsNoTracking() on e.CourseSectionId equals schedule.CourseSectionId
            join section in db.CourseSections on schedule.CourseSectionId equals section.Id
            join offering in db.CourseOfferings on section.CourseOfferingId equals offering.Id
            join subject in db.Subjects on offering.SubjectId equals subject.Id
            join teacher in db.Teachers on schedule.TeacherId equals teacher.Id
            join room in db.Rooms on schedule.RoomId equals room.Id
            where e.UserId == user orderby schedule.Id
            select new ScheduleDto(schedule.Id, section.Id, section.Code, offering.Id, subject.Id, subject.Name,
                teacher.Id, teacher.FirstName + " " + teacher.LastName, room.Id, room.Number,
                schedule.DayOfWeek, schedule.StartTime, schedule.EndTime);
        return await query.Skip(offset).Take(size).ToListAsync(ct);
    }
    public Task<EnrollmentDto> CreateAsync(string? userId, int sectionId, CancellationToken ct)
    {
        var own = ActorId();
        if (userId is not null) RequireAdmin();
        return SaveAsync(null, userId ?? own, sectionId, userId is not null, ct);
    }
    public Task<EnrollmentDto> SwitchAsync(int id, int sectionId, CancellationToken ct) => SaveAsync(id, ActorId(), sectionId, false, ct);

    private async Task<EnrollmentDto> SaveAsync(int? id, string userId, int sectionId, bool assignment, CancellationToken ct)
    {
        if (sectionId <= 0 || (id.HasValue && id <= 0)) throw new EnrollmentException(400, "IDs must be positive.");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            Enrollment item;
            if (id.HasValue)
            {
                item = await db.Enrollments.SingleOrDefaultAsync(x => x.Id == id && (actor.IsAdministrator || x.UserId == userId), ct) ?? throw Missing();
                userId = item.UserId;
            }
            else
            {
                if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) throw Missing();
                item = new Enrollment { UserId = userId };
            }
            // Protect the complete user's enrollment range, including an empty range, against
            // concurrent choices in different offerings under SQL Server serializable isolation.
            await db.Enrollments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(ct);
            var section = await db.CourseSections.SingleOrDefaultAsync(x => x.Id == sectionId, ct) ?? throw Missing();
            if (id.HasValue && item.CourseOfferingId != section.CourseOfferingId) throw Conflict("Switches must stay within the same offering.");
            if (!id.HasValue && await db.Enrollments.AnyAsync(x => x.UserId == userId && x.CourseOfferingId == section.CourseOfferingId, ct))
                throw Conflict("An enrollment already exists for this offering.");
            if (id.HasValue && item.CourseSectionId == sectionId) { await transaction.CommitAsync(ct); return Dto(item); }
            var count = await db.Enrollments.CountAsync(x => x.CourseSectionId == sectionId, ct);
            if (count >= section.Capacity) throw Conflict("The section is full.");
            var target = await (from meeting in db.Schedules.AsNoTracking()
                join offering in db.CourseOfferings on section.CourseOfferingId equals offering.Id
                join term in db.AcademicTerms on offering.AcademicTermId equals term.Id
                where meeting.CourseSectionId == sectionId
                select new { meeting.DayOfWeek, meeting.StartTime, meeting.EndTime, term.StartDate, term.EndDate }).ToListAsync(ct);
            if (target.Count == 0) throw Conflict("The section has no scheduled meetings.");
            var existing = await (from e in db.Enrollments.AsNoTracking()
                join meeting in db.Schedules on e.CourseSectionId equals meeting.CourseSectionId
                join offering in db.CourseOfferings on e.CourseOfferingId equals offering.Id
                join term in db.AcademicTerms on offering.AcademicTermId equals term.Id
                where e.UserId == userId && e.Id != id
                select new { meeting.DayOfWeek, meeting.StartTime, meeting.EndTime, term.StartDate, term.EndDate }).ToListAsync(ct);
            if (target.Any(a => existing.Any(b => a.DayOfWeek == b.DayOfWeek && a.StartTime < b.EndTime && a.EndTime > b.StartTime &&
                MeetingRules.ShareOccurrence(a.StartDate, a.EndDate, b.StartDate, b.EndDate, a.DayOfWeek))))
                throw Conflict("The section conflicts with an existing enrollment.");
            item.CourseOfferingId = section.CourseOfferingId; item.CourseSectionId = sectionId;
            if (!id.HasValue) db.Enrollments.Add(item);
            if (assignment || (id.HasValue && actor.IsAdministrator))
                db.UserNotifications.Add(new UserNotification { UserId = userId, Message = "Your enrollment has been updated by an administrator.", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Dto(item);
        }
        catch (Exception ex) when (DatabaseFailureClassifier.IsConflict(ex)) { throw Conflict("The enrollment changed concurrently; retry the request."); }
    }
}
