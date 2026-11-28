namespace Bastion.BuildingBlocks.Jobs;

public static class IntervalSchedule
{
    // One interval after now; outside the window [from, until) the start of the next window instead.
    public static DateTimeOffset NextRun(DateTimeOffset now, TimeSpan every, TimeOnly? from, TimeOnly? until, TimeZoneInfo timeZone)
    {
        var next = now + every;
        if (from is not { } start || until is not { } end)
        {
            return next;
        }

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(next, timeZone).DateTime);
        var inWindow = start < end ? local >= start && local < end : local >= start || local < end;
        return inWindow ? next : DailySchedule.NextRun(next, start, timeZone);
    }
}
