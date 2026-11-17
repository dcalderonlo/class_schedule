using class_schedule.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace class_schedule.Tests;

public class SchemaTests
{
    private static ClassScheduleDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ClassScheduleDbContext>()
            .UseSqlServer("Server=invalid;Database=metadata;Integrated Security=true;TrustServerCertificate=True")
            .AddInterceptors(new NoConnections())
            .Options);

    private sealed class NoConnections : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            throw new InvalidOperationException("Schema tests must not open a database connection.");

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Schema tests must not open a database connection.");
    }

    [Fact]
    public void Initial_migration_is_registered_and_matches_model()
    {
        using var context = CreateContext();
        Assert.Single(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Initial_sql_script_contains_current_schema_and_constraints()
    {
        using var context = CreateContext();
        var sql = context.GetService<IMigrator>().GenerateScript();
        foreach (var table in new[] { "AspNetRoles", "AspNetUsers", "AspNetRoleClaims", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserRoles", "AspNetUserTokens", "Subjects", "Teachers", "Rooms", "AcademicTerms", "CourseOfferings", "CourseSections", "Schedules", "Enrollments", "UserNotifications" })
            Assert.Contains($"CREATE TABLE [{table}]", sql);
        Assert.Contains("FOREIGN KEY ([CourseSectionId], [CourseOfferingId]) REFERENCES [CourseSections] ([Id], [CourseOfferingId]) ON DELETE NO ACTION", sql);
        foreach (var index in new[] { "IX_Enrollments_UserId_CourseOfferingId", "IX_CourseOfferings_SubjectId_AcademicTermId", "IX_CourseSections_CourseOfferingId_Code", "IX_Subject_Code", "IX_Room_Number", "IX_Teacher_Email", "UserNameIndex", "RoleNameIndex" })
            Assert.Contains($"CREATE UNIQUE INDEX [{index}]", sql);
        foreach (var check in new[] { "StartDate <= EndDate", "WeeklyHours > 0", "Capacity > 0", "CourseOfferingId > 0 AND Capacity > 0", "DayOfWeek >= 0 AND DayOfWeek <= 6 AND StartTime < EndTime" })
            Assert.Contains(check, sql);
        foreach (var relationship in new[] { "FK_Schedules_CourseSections_CourseSectionId", "FK_Schedules_Rooms_RoomId", "FK_Schedules_Teachers_TeacherId", "FK_Enrollments_AspNetUsers_UserId", "FK_UserNotifications_AspNetUsers_UserId" })
        {
            var line = sql.Split('\n').Single(x => x.Contains(relationship));
            Assert.Contains("ON DELETE NO ACTION", line);
        }
    }
}
