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
}