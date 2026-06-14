using System.Collections.Concurrent;
using System.Reflection;
using EbenezerBackend.Shared.CustomAttributes;
using Newtonsoft.Json;

namespace EbenezerBackend.Infrastructure.Data;

public abstract class ArangoDbBaseModel
{
    [JsonProperty("_key", NullValueHandling = NullValueHandling.Ignore)]
    public required string? Key { get; set; }

    public string Id => $"{CollectionName}/{Key}";
    
    private static readonly ConcurrentDictionary<Type, string> CollectionNameCache = new();

    [JsonIgnore]
    private string CollectionName
    {
        get
        {
            var type = this.GetType();

            return CollectionNameCache.GetOrAdd(type, t =>
            {
                var attribute = t.GetCustomAttribute<CollectionNameAttribute>();
                
                if (attribute == null)
                {
                    throw new InvalidOperationException($"A classe {t.Name} não possui o atributo [CollectionName].");
                }

                return attribute.Name; 
            });
        }
    }
}