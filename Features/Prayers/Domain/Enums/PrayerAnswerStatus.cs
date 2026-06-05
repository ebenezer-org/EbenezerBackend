using System.Text.Json.Serialization;

namespace EbenezerBackend.Features.Prayers.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PrayerAnswerStatus
{
    Yes,
    No,
    Wait
}