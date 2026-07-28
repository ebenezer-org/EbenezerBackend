using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Get;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public interface IPrayersService
{
    Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct);
    Task<GetPrayerResponseDto> GetPrayerByIdAsync(string prayerId, CancellationToken ct);
    Task<UpdatePrayerResponseDto> UpdatePrayerAsync(string prayerId, UpdatePrayerRequestDto request, CancellationToken ct);
    Task DeletePrayerAsync(string prayerId, CancellationToken ct);
    Task AddSupportReactionAsync(string prayerId, CancellationToken ct);
    Task RemoveSupportReactionAsync(string prayerId, CancellationToken ct);
    Task<PaginatedResponseDto<TimelinePrayerResponseDto>> GetTimelineAsync(int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyCollection<ListPrayerResponseDto>> SearchPrayersAsync(SearchPrayersRequestDto request, CancellationToken ct);
}
