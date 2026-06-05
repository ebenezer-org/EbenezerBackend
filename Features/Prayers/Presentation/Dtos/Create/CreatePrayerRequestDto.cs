using System.Collections.Generic;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;

public record CreatePrayerRequestDto(
    string Content,
    bool IsPublic = true,
    List<string>? CategoryIds = null
    );
