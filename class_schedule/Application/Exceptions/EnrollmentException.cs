namespace class_schedule.Application.Exceptions;

public sealed class EnrollmentException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
