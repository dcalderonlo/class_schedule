namespace class_schedule.Application.Exceptions;

public sealed class SchedulingException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
