using System.Data;
using class_schedule.Application.DTOs;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Domain.Models.Entities;
using class_schedule.Domain.Scheduling;
using class_schedule.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Application.Services;

public class ScheduleService(ClassScheduleDbContext db) : IScheduleService
{
    private static SchedulingException Invalid(string message) => new(400, message);
    private static SchedulingException Missing() => new(404, "Schedule or referenced record not found.");
    private static SchedulingException Conflict(string message) => new(409, message);

    private IQueryable<ScheduleDto> Projection(IQueryable<Schedule> schedules) =>
        from schedule in schedules
        join section in db.CourseSections on schedule.CourseSectionId equals section.Id
        join offering in db.CourseOfferings on section.CourseOfferingId equals offering.Id
        join subject in db.Subjects on offering.SubjectId equals subject.Id
        join teacher in db.Teachers on schedule.TeacherId equals teacher.Id
        join room in db.Rooms on schedule.RoomId equals room.Id
        select new ScheduleDto(schedule.Id, section.Id, section.Code, offering.Id, subject.Id, subject.Name,
            teacher.Id, teacher.FirstName + " " + teacher.LastName, room.Id, room.Number,
            schedule.DayOfWeek, schedule.StartTime, schedule.EndTime);

    public async Task<IReadOnlyList<ScheduleDto>> GetAllAsync(int offset, int size, CancellationToken ct = default) =>
        await Projection(db.Schedules.AsNoTracking().OrderBy(x => x.Id).Skip(offset).Take(size)).ToListAsync(ct);

    public Task<ScheduleDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Projection(db.Schedules.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(ct);

    public Task<ScheduleDto> CreateAsync(CreateScheduleDto dto, CancellationToken ct = default) => Save(dto, null, ct);
    public Task<ScheduleDto> UpdateAsync(int id, CreateScheduleDto dto, CancellationToken ct = default) => Save(dto, id, ct);

    private async Task<ScheduleDto> Save(CreateScheduleDto dto, int? id, CancellationToken ct)
    {
        if (dto.CourseSectionId <= 0 || dto.TeacherId <= 0 || dto.RoomId <= 0 || (id.HasValue && id <= 0))
            throw Invalid("IDs must be positive.");
        if (!MeetingRules.Valid(dto.DayOfWeek, dto.StartTime, dto.EndTime))
            throw Invalid("A valid weekday and same-day time interval are required.");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            Schedule item;
            if (id.HasValue)
                item = await db.Schedules.SingleOrDefaultAsync(x => x.Id == id.Value, ct) ?? throw Missing();
            else
                item = new Schedule();

            var context = await (from section in db.CourseSections
                join offering in db.CourseOfferings on section.CourseOfferingId equals offering.Id
                join subject in db.Subjects on offering.SubjectId equals subject.Id
                join term in db.AcademicTerms on offering.AcademicTermId equals term.Id
                where section.Id == dto.CourseSectionId
                select new { section.Capacity, subject.WeeklyHours, term.StartDate, term.EndDate }).SingleOrDefaultAsync(ct)
                ?? throw Missing();
            var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == dto.RoomId, ct) ?? throw Missing();
            if (!await db.Teachers.AnyAsync(x => x.Id == dto.TeacherId, ct)) throw Missing();
            if (!MeetingRules.Occurs(context.StartDate, context.EndDate, dto.DayOfWeek))
                throw Invalid("The weekday does not occur in the term.");
            if (room.Capacity < context.Capacity) throw Conflict("The room is too small for the section.");

            // Read the complete section range for load and section overlap. Serializable range locks
            // also protect an empty range against simultaneous insertions on SQL Server.
            var sectionMeetings = await db.Schedules.AsNoTracking().Where(x => x.CourseSectionId == dto.CourseSectionId && x.Id != id)
                .Select(x => new { x.DayOfWeek, x.StartTime, x.EndTime }).ToListAsync(ct);
            decimal ticks = dto.EndTime.Ticks - dto.StartTime.Ticks;
            foreach (var meeting in sectionMeetings)
            {
                ticks += meeting.EndTime.Ticks - meeting.StartTime.Ticks;
                if (meeting.DayOfWeek == dto.DayOfWeek && meeting.StartTime < dto.EndTime && meeting.EndTime > dto.StartTime)
                    throw Conflict("The section already has a meeting at this time.");
            }
            if (ticks > (decimal)context.WeeklyHours * TimeSpan.TicksPerHour)
                throw Conflict("The section exceeds the subject's weekly hours.");

            // Query each resource separately so serializable locking covers both indexed ranges.
            foreach (var byRoom in new[] { true, false })
            {
                var candidates = byRoom
                    ? db.Schedules.AsNoTracking().Where(x => x.RoomId == dto.RoomId)
                    : db.Schedules.AsNoTracking().Where(x => x.TeacherId == dto.TeacherId);
                var dates = await (from meeting in candidates
                    join section in db.CourseSections on meeting.CourseSectionId equals section.Id
                    join offering in db.CourseOfferings on section.CourseOfferingId equals offering.Id
                    join term in db.AcademicTerms on offering.AcademicTermId equals term.Id
                    where meeting.Id != id && meeting.DayOfWeek == dto.DayOfWeek && meeting.StartTime < dto.EndTime && meeting.EndTime > dto.StartTime
                    select new { term.StartDate, term.EndDate }).ToListAsync(ct);
                if (dates.Any(x => MeetingRules.ShareOccurrence(context.StartDate, context.EndDate, x.StartDate, x.EndDate, dto.DayOfWeek)))
                    throw Conflict(byRoom ? "The room is occupied." : "The teacher is occupied.");
            }

            // A new target meeting must not overlap another offering of any enrolled user.
            var enrolledUsers = await db.Enrollments.AsNoTracking().Where(x => x.CourseSectionId == dto.CourseSectionId)
                .Select(x => x.UserId).Distinct().ToListAsync(ct);
            if (enrolledUsers.Count > 0)
            {
                var otherMeetings = await (from enrollment in db.Enrollments.AsNoTracking()
                    join meeting in db.Schedules on enrollment.CourseSectionId equals meeting.CourseSectionId
                    join offering in db.CourseOfferings on enrollment.CourseOfferingId equals offering.Id
                    join term in db.AcademicTerms on offering.AcademicTermId equals term.Id
                    where enrolledUsers.Contains(enrollment.UserId) && enrollment.CourseOfferingId !=
                        db.CourseSections.Where(s => s.Id == dto.CourseSectionId).Select(s => s.CourseOfferingId).First()
                        && meeting.Id != id && meeting.DayOfWeek == dto.DayOfWeek && meeting.StartTime < dto.EndTime && meeting.EndTime > dto.StartTime
                    select new { term.StartDate, term.EndDate }).ToListAsync(ct);
                if (otherMeetings.Any(x => MeetingRules.ShareOccurrence(context.StartDate, context.EndDate, x.StartDate, x.EndDate, dto.DayOfWeek)))
                    throw Conflict("The meeting conflicts with an enrolled user's schedule.");
            }
            bool changed = !id.HasValue || item.CourseSectionId != dto.CourseSectionId || item.TeacherId != dto.TeacherId ||
                item.RoomId != dto.RoomId || item.DayOfWeek != dto.DayOfWeek || item.StartTime != dto.StartTime || item.EndTime != dto.EndTime;
            int oldSection = item.CourseSectionId;
            item.CourseSectionId = dto.CourseSectionId;
            item.TeacherId = dto.TeacherId;
            item.RoomId = dto.RoomId;
            item.DayOfWeek = dto.DayOfWeek;
            item.StartTime = dto.StartTime;
            item.EndTime = dto.EndTime;
            if (!id.HasValue) db.Schedules.Add(item);
            if (changed)
            {
                var affected = await db.Enrollments.AsNoTracking()
                    .Where(x => x.CourseSectionId == oldSection || x.CourseSectionId == dto.CourseSectionId)
                    .Select(x => x.UserId).Distinct().ToListAsync(ct);
                db.UserNotifications.AddRange(affected.Select(userId => new UserNotification
                {
                    UserId = userId, Message = "A meeting in your schedule has changed.", CreatedAtUtc = DateTime.UtcNow
                }));
            }
            await db.SaveChangesAsync(ct);
            var result = await GetByIdAsync(item.Id, ct) ?? throw Missing();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw Conflict("The schedule changed concurrently; retry the request."); }
        catch (Exception ex) when (DatabaseFailureClassifier.IsLockConflict(ex)) { throw Conflict("The schedule changed concurrently; retry the request."); }
        catch (Exception ex) when (DatabaseFailureClassifier.IsForeignKeyConflict(ex)) { throw Missing(); }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var item = await db.Schedules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
            var affected = await db.Enrollments.AsNoTracking().Where(x => x.CourseSectionId == item.CourseSectionId)
                .Select(x => x.UserId).Distinct().ToListAsync(ct);
            db.Schedules.Remove(item);
            db.UserNotifications.AddRange(affected.Select(userId => new UserNotification
            {
                UserId = userId, Message = "A meeting in your schedule has changed.", CreatedAtUtc = DateTime.UtcNow
            }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw Conflict("The schedule changed concurrently; retry the request."); }
        catch (Exception ex) when (DatabaseFailureClassifier.IsLockConflict(ex)) { throw Conflict("The schedule changed concurrently; retry the request."); }
        catch (Exception ex) when (DatabaseFailureClassifier.IsForeignKeyConflict(ex)) { throw Missing(); }
    }
}
