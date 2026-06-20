namespace EbenezerBackend.Shared.Data;

public static class DbUtils
{
    public static string FormatDateTimeToIso8601(DateTime date)
    {
        return date.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
