using System;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Prayers.Data.Models;

[BsonIgnoreExtraElements]
public class PrayerSupportReactionModel
{
    public string UserName { get; set; } = string.Empty;
    public DateTime ReactedAt { get; set; }
}
