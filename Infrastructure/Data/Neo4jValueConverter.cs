using System.Globalization;
using Neo4j.Driver;

namespace EbenezerBackend.Infrastructure.Data;

public static class Neo4JValueConverter
{
    public static string? ToIso8601(DateTime? value) => value?.ToString("O");

    public static DateTime ToDateTime(object value) => value switch
    {
        string s          => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind),
        LocalDateTime ldt => new DateTime(ldt.Year, ldt.Month, ldt.Day,
                                          ldt.Hour, ldt.Minute, ldt.Second,
                                          ldt.Nanosecond / 1_000_000, DateTimeKind.Utc),
        ZonedDateTime zdt => zdt.ToDateTimeOffset().UtcDateTime,
        _                 => DateTime.Parse(value.ToString()!, null, DateTimeStyles.RoundtripKind)
    };

    public static DateTime? ToNullableDateTime(object? value)
        => value is null ? null : ToDateTime(value);
}
