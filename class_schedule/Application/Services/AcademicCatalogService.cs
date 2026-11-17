using class_schedule.Application.DTOs.Catalog;
using class_schedule.Application.Exceptions;
using class_schedule.Application.Interfaces;
using class_schedule.Domain.Models.Entities;
using class_schedule.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace class_schedule.Application.Services;

public class AcademicCatalogService(ClassScheduleDbContext db) : IAcademicCatalogService
{
    private static CatalogException Duplicate() => new(409, "A catalog record already exists.");
    private static CatalogException Missing() => new(404, "A referenced catalog record was not found.");
    private static string Code(string value) => value.Trim().ToUpperInvariant();

    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        { throw Duplicate(); }
    }

    public async Task<object> List(string resource, int offset, int size, CancellationToken ct) => resource switch
    {
        "subjects" => await db.Subjects.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new SubjectDto(x.Id, x.Name, x.Code, x.WeeklyHours)).ToListAsync(ct),
        "teachers" => await db.Teachers.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new TeacherDto(x.Id, x.FirstName + " " + x.LastName)).ToListAsync(ct),
        "rooms" => await db.Rooms.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new RoomDto(x.Id, x.Number, x.Capacity, x.Type)).ToListAsync(ct),
        "terms" => await db.AcademicTerms.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new TermDto(x.Id, x.Name, x.StartDate, x.EndDate)).ToListAsync(ct),
        "offerings" => await db.CourseOfferings.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new OfferingDto(x.Id, x.SubjectId, x.AcademicTermId)).ToListAsync(ct),
        "sections" => await db.CourseSections.OrderBy(x => x.Id).Skip(offset).Take(size).Select(x => new SectionDto(x.Id, x.CourseOfferingId, x.Code, x.Capacity)).ToListAsync(ct),
        _ => throw Missing()
    };

    public async Task<SubjectDto> Create(CreateSubject input, CancellationToken ct)
    {
        var code = Code(input.Code);
        if (await db.Subjects.AnyAsync(x => x.Code.ToUpper() == code, ct)) throw Duplicate();
        var item = new Subject { Name = input.Name.Trim(), Code = code, WeeklyHours = input.WeeklyHours };
        db.Subjects.Add(item); await Save(ct);
        return new(item.Id, item.Name, item.Code, item.WeeklyHours);
    }
    public async Task<TeacherDto> Create(CreateTeacher input, CancellationToken ct)
    {
        var email = input.Email.Trim().ToUpperInvariant();
        if (await db.Teachers.AnyAsync(x => x.Email.ToUpper() == email, ct)) throw Duplicate();
        var item = new Teacher { FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), Email = email };
        db.Teachers.Add(item); await Save(ct);
        return new(item.Id, item.FullName);
    }
    public async Task<RoomDto> Create(CreateRoom input, CancellationToken ct)
    {
        var number = Code(input.Number);
        if (await db.Rooms.AnyAsync(x => x.Number.ToUpper() == number, ct)) throw Duplicate();
        var item = new Room { Number = number, Capacity = input.Capacity, Type = input.Type };
        db.Rooms.Add(item); await Save(ct);
        return new(item.Id, item.Number, item.Capacity, item.Type);
    }
    public async Task<TermDto> Create(CreateTerm input, CancellationToken ct)
    {
        if (input.StartDate == default || input.EndDate < input.StartDate) throw new CatalogException(400, "Invalid term dates.");
        var item = new AcademicTerm(input.Name, input.StartDate, input.EndDate);
        db.AcademicTerms.Add(item); await Save(ct);
        return new(item.Id, item.Name, item.StartDate, item.EndDate);
    }
    public async Task<OfferingDto> Create(CreateOffering input, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(x => x.Id == input.SubjectId, ct) || !await db.AcademicTerms.AnyAsync(x => x.Id == input.AcademicTermId, ct)) throw Missing();
        if (await db.CourseOfferings.AnyAsync(x => x.SubjectId == input.SubjectId && x.AcademicTermId == input.AcademicTermId, ct)) throw Duplicate();
        var item = new CourseOffering(input.SubjectId, input.AcademicTermId);
        db.CourseOfferings.Add(item); await Save(ct);
        return new(item.Id, item.SubjectId, item.AcademicTermId);
    }
    public async Task<SectionDto> Create(CreateSection input, CancellationToken ct)
    {
        if (!await db.CourseOfferings.AnyAsync(x => x.Id == input.CourseOfferingId, ct)) throw Missing();
        var code = Code(input.Code);
        if (await db.CourseSections.AnyAsync(x => x.CourseOfferingId == input.CourseOfferingId && x.Code.ToUpper() == code, ct)) throw Duplicate();
        var item = new CourseSection(input.CourseOfferingId, code, input.Capacity);
        db.CourseSections.Add(item); await Save(ct);
        return new(item.Id, item.CourseOfferingId, item.Code, item.Capacity);
    }
}
