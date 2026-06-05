using System;

namespace EbenezerBackend.Features.Prayers.Domain.Entities;

public class PrayerEntity(
    string content,
    bool isPublic,
    string? id = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null,
    string? authorResponseStatus = null,
    string? authorResponseMessage = null,
    DateTime? authorResponseCreatedAt = null)
{
    public string? Id { get; set; } = id;
    public string Content { get; private set; } = content;
    public bool IsPublic { get; private set; } = isPublic;
    public DateTime CreatedAt { get; init; } = createdAt ?? DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = updatedAt ?? createdAt ?? DateTime.UtcNow;
    public string? AuthorResponseStatus { get; private set; } = authorResponseStatus;
    public string? AuthorResponseMessage { get; private set; } = authorResponseMessage;
    public DateTime? AuthorResponseCreatedAt { get; private set; } = authorResponseCreatedAt;

    public void Update(string content, bool isPublic)
    {
        Content = content;
        IsPublic = isPublic;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAuthorResponse(string status, string? message)
    {
        AuthorResponseStatus = status;
        AuthorResponseMessage = message;
        AuthorResponseCreatedAt = DateTime.UtcNow;
    }
}