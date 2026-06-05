using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.AuthorResponse;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Get;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Support;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public interface IPrayersService
{
    Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct);
    Task<GetPrayerResponseDto> GetPrayerByIdAsync(string prayerId, CancellationToken ct);
    Task<UpdatePrayerResponseDto> UpdatePrayerAsync(string prayerId, UpdatePrayerRequestDto request, CancellationToken ct);
    Task DeletePrayerAsync(string prayerId, CancellationToken ct);
    Task<SupportReactionResponseDto> AddSupportReactionAsync(string prayerId, CancellationToken ct);
    Task<PaginatedResponseDto<IReadOnlyCollection<TimelinePrayerResponseDto>>> GetTimelineAsync(int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyCollection<ListPrayerResponseDto>> SearchPrayersAsync(SearchPrayersRequestDto request, CancellationToken ct);
}
