using Serilog.Core;
using Serilog.Events;

namespace DiscogScrobblerMVC.Services.Utilities;

internal sealed class LogDisplayTimeEnricher : ILogEventEnricher
{
    private readonly TimeZoneInfo? _timeZone;

    public LogDisplayTimeEnricher(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            _timeZone = null;
        }
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var display = _timeZone is null
            ? logEvent.Timestamp.LocalDateTime
            : TimeZoneInfo.ConvertTime(logEvent.Timestamp, _timeZone).DateTime;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DisplayTimestamp", display));
    }
}
