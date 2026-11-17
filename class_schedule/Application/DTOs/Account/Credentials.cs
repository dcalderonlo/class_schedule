using System.ComponentModel.DataAnnotations;

namespace class_schedule.Application.DTOs.Account;

public sealed record Credentials(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
