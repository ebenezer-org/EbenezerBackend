using System;
using System.Text.RegularExpressions;

namespace EbenezerBackend.Features.Categories.Domain.Entities;

public class CategoryEntity
{
    private const int MaxNameLength = 80;
    private const int MaxDescriptionLength = 500;

    public string? Id { get; set; }
    public string OwnerUsername { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string ColorHex { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; private set; }

    public CategoryEntity(
        string ownerUsername,
        string name,
        string description,
        string colorHex,
        string? id = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        Id = id;
        OwnerUsername = ValidateOwnerUsername(ownerUsername);
        Name = ValidateName(name);
        Description = ValidateDescription(description);
        ColorHex = NormalizeColorHex(colorHex);
        CreatedAt = createdAt ?? DateTime.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;
    }

    public void Update(string name, string description, string colorHex)
    {
        Name = ValidateName(name);
        Description = ValidateDescription(description);
        ColorHex = NormalizeColorHex(colorHex);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidateOwnerUsername(string? ownerUsername)
    {
        if (string.IsNullOrWhiteSpace(ownerUsername))
        {
            throw new ArgumentException("Usuário dono da categoria é obrigatório");
        }

        return ownerUsername.Trim();
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da categoria é obrigatório");
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException($"Nome da categoria deve ter no máximo {MaxNameLength} caracteres");
        }

        return normalizedName;
    }

    private static string ValidateDescription(string? description)
    {
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Descrição da categoria deve ter no máximo {MaxDescriptionLength} caracteres");
        }

        return normalizedDescription;
    }

    private static string NormalizeColorHex(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            throw new ArgumentException("Cor da categoria é obrigatória");
        }

        var normalizedColor = colorHex.Trim();

        if (!normalizedColor.StartsWith('#'))
        {
            normalizedColor = $"#{normalizedColor}";
        }

        if (!Regex.IsMatch(normalizedColor, "^#[0-9A-Fa-f]{6}$"))
        {
            throw new ArgumentException("Cor da categoria deve estar no formato HEX #RRGGBB");
        }

        return normalizedColor.ToUpperInvariant();
    }
}
