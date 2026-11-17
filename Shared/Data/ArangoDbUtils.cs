namespace EbenezerBackend.Shared.Data;

public static class ArangoDbUtils
{
    public static string? BuildArangoDbId(string? keyOrId, string collectionName) 
    {
        if (string.IsNullOrEmpty(keyOrId))
        {
            return null;
        }

        if (keyOrId.Contains('/'))
        {
            return keyOrId;
        }

        return $"{collectionName}/{keyOrId}";
    }
    
    public static string? ExtractKey(string? keyOrId, string collectionName)
    {
        if (string.IsNullOrEmpty(keyOrId))
        {
            return null;
        }

        var separatorIndex = keyOrId.IndexOf('/');

        if (separatorIndex < 0)
        {
            return keyOrId;
        }

        return keyOrId[..separatorIndex] == collectionName
            ? keyOrId[(separatorIndex + 1)..]
            : null;
    }

    public static string FormatDateTimeToArangoDbFormat(DateTime date)
    {
        return date.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}