using Microsoft.Extensions.Options;

namespace ScrapManagement.Web.Infrastructure;

public interface IClock
{
    DateTime UtcNow { get; }
    /// <summary>Today's date in the configured shop time zone.</summary>
    DateOnly Today { get; }
    DateTime ToLocal(DateTime utc);
}

public class SystemClock : IClock
{
    private readonly TimeZoneInfo _tz;

    public SystemClock(IOptions<AppOptions> options)
    {
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId); }
        catch (Exception) { _tz = TimeZoneInfo.Utc; }
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));

    public DateTime ToLocal(DateTime utc)
    {
        var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(u, _tz);
    }
}
