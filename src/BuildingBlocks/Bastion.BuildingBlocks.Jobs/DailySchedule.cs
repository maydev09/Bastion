namespace Bastion.BuildingBlocks.Jobs;

public static class DailySchedule
{
    // The first moment after now when the clock in the time zone shows the given time.
    public static DateTimeOffset NextRun(DateTimeOffset now, TimeOnly at, TimeZoneInfo timeZone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var next = At(today, at, timeZone);
        return next > now ? next : At(today.AddDays(1), at, timeZone);
    }

    private static DateTimeOffset At(DateOnly day, TimeOnly at, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(at);

        // A time skipped when clocks go forward (e.g. 02:30 in spring) runs an hour later.
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
