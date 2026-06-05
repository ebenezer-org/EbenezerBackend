using System.Collections.Generic;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;

public record UpdatePrayerRequestDto(
    string Content,
    bool IsPublic,
    IReadOnlyCollection<string> CategoryIds
);

