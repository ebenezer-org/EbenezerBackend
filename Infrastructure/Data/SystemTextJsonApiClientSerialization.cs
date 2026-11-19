using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using ArangoDBNetStandard.Serialization;

namespace EbenezerBackend.Infrastructure.Data;

/// <summary>
/// Serialização do cliente ArangoDB baseada em System.Text.Json.
///
/// O serializador padrão do ArangoDBNetStandard (Newtonsoft) cria um JsonSerializerSettings e um
/// ContractResolver novos a cada requisição, o que descarta o cache de contratos e custa CPU da API
/// em todas as consultas. Esta implementação mantém as opções em cache e reproduz o comportamento
/// do serializador original:
///  - corpo da requisição: camelCase e omissão de nulos conforme as opções recebidas;
///  - valores de bind vars (Dictionary&lt;string, object&gt;): nomes originais (PascalCase) e nulos preservados;
///  - leitura: nomes sem diferenciar maiúsculas/minúsculas, campos públicos incluídos
///    e membros `required` tratados como opcionais (o Newtonsoft ignora `required`).
/// </summary>
public sealed class SystemTextJsonApiClientSerialization : ApiClientSerialization
{
    private static readonly JsonSerializerOptions ReadOptions = CreateReadOptions();

    private static readonly JsonSerializerOptions BindVarValueOptions = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true
    };

    private static readonly ConcurrentDictionary<(bool Camel, bool IgnoreNull, bool StringEnum, bool ApplyToDictionary), JsonSerializerOptions>
        WriteOptionsCache = new();

    public override T DeserializeFromStream<T>(Stream stream)
    {
        if (stream is null || !stream.CanRead || IsEmpty(stream))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(stream, ReadOptions)!;
    }

    public override async Task<T> DeserializeFromStreamAsync<T>(Stream stream)
    {
        if (stream is null || !stream.CanRead || IsEmpty(stream))
        {
            return default!;
        }

        return (await JsonSerializer.DeserializeAsync<T>(stream, ReadOptions).ConfigureAwait(false))!;
    }

    public override byte[] Serialize<T>(T item, ApiClientSerializationOptions? serializationOptions)
        => JsonSerializer.SerializeToUtf8Bytes(item, RuntimeTypeOf(item), GetWriteOptions(serializationOptions));

    public override Task<byte[]> SerializeAsync<T>(T item, ApiClientSerializationOptions? serializationOptions)
        => Task.FromResult(Serialize(item, serializationOptions));

    public override string SerializeToString<T>(T item, ApiClientSerializationOptions? serializationOptions)
        => JsonSerializer.Serialize(item, RuntimeTypeOf(item), GetWriteOptions(serializationOptions));

    public override Task<string> SerializeToStringAsync<T>(T item, ApiClientSerializationOptions? serializationOptions)
        => Task.FromResult(SerializeToString(item, serializationOptions));

    private static bool IsEmpty(Stream stream) => stream.CanSeek && stream.Length == 0;

    // O Newtonsoft serializa pelo tipo em tempo de execução; mantemos o mesmo comportamento.
    private static Type RuntimeTypeOf<T>(T item) => item?.GetType() ?? typeof(T);

    private JsonSerializerOptions GetWriteOptions(ApiClientSerializationOptions? options)
    {
        options ??= DefaultOptions;

        var key = (
            options.UseCamelCasePropertyNames,
            options.IgnoreNullValues,
            options.UseStringEnumConversion,
            options.ApplySerializationOptionsToDictionaryValues);

        return WriteOptionsCache.GetOrAdd(key, static k => CreateWriteOptions(k.Camel, k.IgnoreNull, k.StringEnum, k.ApplyToDictionary));
    }

    private static JsonSerializerOptions CreateWriteOptions(bool camelCase, bool ignoreNull, bool stringEnum, bool applyToDictionary)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = ignoreNull ? JsonIgnoreCondition.WhenWritingNull : JsonIgnoreCondition.Never,
            IncludeFields = true
        };

        if (stringEnum)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        if (!applyToDictionary)
        {
            options.Converters.Add(new BindVarsConverter());
        }

        return options;
    }

    private static JsonSerializerOptions CreateReadOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                property.IsRequired = false;
            }
        });

        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            TypeInfoResolver = resolver
        };
    }

    /// <summary>
    /// Escreve os bind vars sem aplicar camelCase nem omitir nulos nos valores,
    /// como o DictionaryValueConverter do driver original.
    /// </summary>
    private sealed class BindVarsConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("BindVarsConverter é usado apenas para escrita.");

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var (name, item) in value)
            {
                writer.WritePropertyName(name);

                if (item is null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                JsonSerializer.Serialize(writer, item, item.GetType(), BindVarValueOptions);
            }

            writer.WriteEndObject();
        }
    }
}
