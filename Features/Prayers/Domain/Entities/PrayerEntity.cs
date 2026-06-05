using System;

namespace EbenezerBackend.Features.Prayers.Domain.Entities;

public class PrayerEntity(string content)
{
    public string Content { get; set; } = content;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}