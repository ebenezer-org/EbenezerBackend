using System.Text.Json.Serialization;

namespace EbenezerBackend.Features.Retrospective.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EncouragementMessageCategoryEnum
{
    Sovereignty, // Maioria de respostas "Não"
    Patience,    // Maioria de respostas "Aguarde"
    Gratitude,   // Maioria de respostas "Sim"
    Default      // Meio termo / Equilibrado
}