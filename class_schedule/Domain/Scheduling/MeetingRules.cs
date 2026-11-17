namespace class_schedule.Domain.Scheduling;

public static class MeetingRules
{
    public static bool Valid(DayOfWeek day, TimeOnly start, TimeOnly end) =>
        Enum.IsDefined(day) && start < end;

    public static bool Occurs(DateOnly start, DateOnly end, DayOfWeek day)
    {
        if (start > end) return false;
        var daysUntil = ((int)day - (int)start.DayOfWeek + 7) % 7;
        return end.DayNumber - start.DayNumber >= daysUntil;
    }

    public static bool ShareOccurrence(DateOnly firstStart, DateOnly firstEnd, DateOnly secondStart, DateOnly secondEnd, DayOfWeek day) =>
        Occurs(firstStart > secondStart ? firstStart : secondStart,
            firstEnd < secondEnd ? firstEnd : secondEnd, day);
}
