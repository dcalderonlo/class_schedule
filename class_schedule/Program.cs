using class_schedule.Components;
using class_schedule.Application.Interfaces;
using class_schedule.Application.Security;
using class_schedule.Application.Services;
using class_schedule.Infrastructure.Data;
using class_schedule.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<ClassScheduleDbContext>(options =>
{
    var connection = builder.Configuration.GetConnectionString("ClassSchedule");
    if (!string.IsNullOrWhiteSpace(connection)) options.UseSqlServer(connection);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IAcademicCatalogService, AcademicCatalogService>();
builder.Services.AddScheduleIdentity();
builder.Services.AddHostedService<DevelopmentAdminInitializer>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch =>
        branch.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
        })));
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
        branch.UseExceptionHandler("/Error", createScopeForErrors: true));
    app.UseHsts();
}
// API failures must remain status codes, not template pages.
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
    branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
public partial class Program { }
