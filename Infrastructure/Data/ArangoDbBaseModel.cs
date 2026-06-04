using Newtonsoft.Json;

namespace EbenezerBackend.Infrastructure.Data;

public abstract class ArangoDbBaseModel
{
    [JsonProperty("_key", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }
}