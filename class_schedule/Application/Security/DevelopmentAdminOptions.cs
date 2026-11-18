namespace class_schedule.Application.Security;

public sealed class DevelopmentAdminOptions
{
    public bool Enabled { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}
