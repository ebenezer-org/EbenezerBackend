namespace EbenezerBackend.Shared.Data;

public static class DbUtils
{
    /// <summary>
    /// Builds a composite "collection/key" identifier from a plain key or returns the
    /// value unchanged if it already contains a slash (i.e., is already a full id).
    /// </summary>
    public static string? BuildDbId(string? keyOrId, string collectionName)
    {
        if (string.IsNullOrEmpty(keyOrId))
        {
            return null;
        }

        return keyOrId.Contains('/') ? keyOrId : $"{collectionName}/{keyOrId}";
    }

    public static string FormatDateTimeToIso8601(DateTime date)
    {
        return date.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
