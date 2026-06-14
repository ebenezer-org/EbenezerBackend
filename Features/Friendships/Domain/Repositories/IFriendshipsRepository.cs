using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;

namespace EbenezerBackend.Features.Friendships.Domain.Repositories;

public interface IFriendshipsRepository
{
    Task<(IReadOnlyCollection<FriendshipRequestEntity> Items, int TotalCount)> SearchFriendships(
        CancellationToken ct,
        int page,
        int pageSize,
        string? fromUserId = null,
        string? toUserId = null,
        (string? UserA, string? UserB)? containsUserIds = null,
        FriendshipStatusEnum? status = null
        );
    Task<FriendshipRequestEntity> InsertFriendshipRequestAsync(string fromUserId, string toUserId, CancellationToken ct);
    Task<FriendshipRequestEntity?> UpdateFriendshipRequestAsync(FriendshipRequestEntity entity, CancellationToken ct);
    Task<FriendshipRequestEntity?> FindFriendshipRequestByIdAsync(string requestId, CancellationToken ct);
    Task<FriendshipRequestEntity?> DeleteFriendshipRequestByIdAsync(string requestId, CancellationToken ct);
    Task<(IReadOnlyCollection<FriendSuggestionRepositoryDto> Items, int TotalCount)> GetFriendSuggestionsAsync(
        string userId,
        int depth,
        int page,
        int pageSize,
        CancellationToken ct
        );
    Task<(IReadOnlyCollection<FriendProfileRepositoryDto> Items, int TotalCount)> GetMutualFriendsAsync(
        string userIdA,
        string userIdB, 
        int page,
        int pageSize,
        CancellationToken ct
        );
}
