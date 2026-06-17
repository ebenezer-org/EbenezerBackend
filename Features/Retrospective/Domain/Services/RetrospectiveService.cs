using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Repositories;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;
using EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Features.Retrospective.Domain.Services;

public class RetrospectiveService(IUserContext userContext, IRetrospectiveRepository repository) : IRetrospectiveService
{
    
    public async Task<GetMyRetrospectiveResponseDto> GetMyRetrospectiveAsync(GetMyRetrospectiveRequestDto request, CancellationToken ct)
    {
        var metrics = await repository.GetMetrics(userId: userContext.Id, fromDate: request.FromDate, toDate: request.ToDate, ct: ct);

        var encouragementMessageCategory = DetermineCategory(metrics);
        
        var encouragementMessage = await repository.FindEncouragementMessagesByCategoriesAsync(encouragementMessageCategory, ct);

        return new GetMyRetrospectiveResponseDto
        (
            Title: encouragementMessage.Title,
            Message: encouragementMessage.Message,
            ScriptureReference: encouragementMessage.ScriptureReference,
            ScriptureVerse: encouragementMessage.ScriptureVerse,
            Metrics: metrics
        );
    }

    private static EncouragementMessageCategoryEnum DetermineCategory(PrayersMetricsResponseDto? metrics)
    {
        if (metrics is null || metrics.Summary.GrantedCount == metrics.Summary.SovereignNoCount && metrics.Summary.GrantedCount == metrics.Summary.WaitCount)
            return EncouragementMessageCategoryEnum.Default;

        if (metrics.Summary.GrantedCount > metrics.Summary.SovereignNoCount && metrics.Summary.GrantedCount > metrics.Summary.WaitCount)
        {
            return EncouragementMessageCategoryEnum.Gratitude;
        }
        
        if (metrics.Summary.SovereignNoCount > metrics.Summary.GrantedCount && metrics.Summary.SovereignNoCount > metrics.Summary.WaitCount)
        {
            return EncouragementMessageCategoryEnum.Sovereignty;
        }
        
        return EncouragementMessageCategoryEnum.Patience;
    }
}