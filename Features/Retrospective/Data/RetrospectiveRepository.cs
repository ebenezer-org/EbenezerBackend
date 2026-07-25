using ArangoDBNetStandard;
using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Data.Models;
using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Repositories;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Retrospective.Data;

public class RetrospectiveRepository(IArangoDBClient db) : BaseRepository<EncouragementMessageModel>, IRetrospectiveRepository
{
    public async Task<PrayersMetricsResponseDto?> GetMetrics(string userId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        var query = $@"
            // 1. Definição do escopo inicial de orações do usuário no período
            LET prayers = (
                FOR p IN 1..1 OUTBOUND @userId {ArangoDbEdges.PostedBy}
                    FILTER p.CreatedAt >= @startDate AND p.CreatedAt <= @endDate
                    RETURN p
            )
            
            // ==========================================
            // BLOCO 1: SUMÁRIO DE ORAÇÕES
            // ==========================================
            LET totalPrayers = LENGTH(prayers)
            LET grantedCount = LENGTH(FOR p IN prayers FILTER p.DivineAnswerStatus == ""{nameof(PrayerAnswerStatusEnum.Granted)}"" RETURN 1)
            LET sovereignNoCount = LENGTH(FOR p IN prayers FILTER p.DivineAnswerStatus == ""{nameof(PrayerAnswerStatusEnum.SovereignNo)}"" RETURN 1)
            LET waitCount = LENGTH(FOR p IN prayers FILTER p.DivineAnswerStatus == ""{nameof(PrayerAnswerStatusEnum.Wait)}"" RETURN 1)
            LET answeredCount = LENGTH(FOR p IN prayers FILTER p.DivineAnswerStatus != null RETURN 1)

            // ==========================================
            // BLOCO 2: INSIGHTS DE CATEGORIAS
            // ==========================================
            LET defaultCategoryItem = {{ Name: ""Nenhuma"", Count: 0 }}

            // Categorias mais e menos pedidas
            LET generalCategoryCounts = (
                FOR p IN prayers
                    FILTER p.Category != null
                    COLLECT category = p.Category WITH COUNT INTO total
                    RETURN {{ Name: category, Count: total }}
            )
            LET mostRequested = (FOR category IN generalCategoryCounts SORT category.Count DESC LIMIT 1 RETURN category)[0]
            LET leastRequested = (FOR category IN generalCategoryCounts SORT category.Count ASC LIMIT 1 RETURN category)[0]

            // Categorias mais respondidas (Sim ou Não)
            LET answeredCategoryCounts = (
                FOR p IN prayers
                    FILTER p.DivineAnswerStatus != null
                    FILTER p.Category != null
                    COLLECT category = p.Category WITH COUNT INTO total
                    RETURN {{ Name: category, Count: total }}
            )
            LET mostAnswered = (FOR c IN answeredCategoryCounts SORT c.Count DESC LIMIT 1 RETURN c)[0]

            // ==========================================
            // BLOCO 3: COMUNIDADE E ENCORAJAMENTO
            // ==========================================
            // Novos amigos (Navegando pela aresta e filtrando as propriedades da amizade 'e')
            LET newFriendsCount = LENGTH(
                FOR v, e IN 1..1 ANY @userId {ArangoDbEdges.Friendships}
                    FILTER e.AcceptedAt >= @startDate AND e.AcceptedAt <= @endDate
                    RETURN 1
            )

            // Coleta de interações (Comentários e Reações) 
            // Usamos grafos nativos para máxima performance sem table scans
            LET interactions = FLATTEN(
                FOR p IN prayers
                    LET commenters = (
                        FOR v IN 1..1 ANY p._id {ArangoDbEdges.CommentedOn}
                            FILTER v.AuthorId != @userId
                            RETURN v.AuthorId
                    )
                    LET reactors = (
                        FOR v IN 1..1 ANY p._id {ArangoDbEdges.ReactedBy}
                            FILTER v._id != @userId
                            RETURN v._id
                    )
                    RETURN APPEND(commenters, reactors)
            )

            LET totalEncouragementsReceived = LENGTH(interactions)
            
            // UNIQUE resolve os parceiros únicos de intercessão
            LET uniqueIntercessors = UNIQUE(interactions)
            LET intercessionPartnersCount = LENGTH(uniqueIntercessors)

            // ==========================================
            // PROJEÇÃO FINAL
            // ==========================================
            RETURN {{
                Summary: {{
                    TotalPrayers: totalPrayers,
                    AnsweredCount: answeredCount,
                    GrantedCount: grantedCount,
                    SovereignNoCount: sovereignNoCount,
                    WaitCount: waitCount
                }},
                Categories: {{
                    MostRequestedCategory: mostRequested != null ? mostRequested : defaultCategoryItem,
                    LeastRequestedCategory: leastRequested != null ? leastRequested : defaultCategoryItem,
                    MostAnsweredCategory: mostAnswered != null ? mostAnswered : defaultCategoryItem
                }},
                Fellowship: {{
                    NewFriendsCount: newFriendsCount,
                    IntercessionPartnersCount: intercessionPartnersCount,
                    TotalEncouragementsReceived: totalEncouragementsReceived
                }}
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userId", ArangoDbUtils.BuildArangoDbId(userId, ArangoDbCollections.Users)! },
            { "startDate", ArangoDbUtils.FormatDateTimeToArangoDbFormat(fromDate) },
            { "endDate", ArangoDbUtils.FormatDateTimeToArangoDbFormat(toDate) }
        };

        var response = await db.Cursor.PostCursorAsync<PrayersMetricsResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
    }

    public async Task<EncouragementMessageEntity> FindEncouragementMessagesByCategoriesAsync(
        EncouragementMessageCategoryEnum encouragementMessageCategory, CancellationToken ct)
    {
        var encouragementMessageId = ArangoDbUtils.BuildArangoDbId(encouragementMessageCategory.ToString(), CollectionName);

        var encouragementMessageModel = await db.Document.GetDocumentAsync<EncouragementMessageModel>(encouragementMessageId, token: ct);

        return encouragementMessageModel.ToEntity();
    }
}