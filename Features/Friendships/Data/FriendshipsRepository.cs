using EbenezerBackend.Features.Friendships.Data.Models;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Friendships.Data;

public class FriendshipsRepository : BaseRepository<FriendshipEdgeModel>, IFriendshipsRepository
{
    public Task<(IReadOnlyCollection<FriendshipRequestEntity> Items, int TotalCount)> SearchFriendships(
        CancellationToken ct,
        int page,
        int pageSize,
        string? fromUserId = null,
        string? toUserId = null,
        (string? UserA, string? UserB)? containsUserIds = null,
        FriendshipStatusEnum? status = null)
        => throw new NotImplementedException();

    public Task<FriendshipRequestEntity> InsertFriendshipRequestAsync(string fromUserId, string toUserId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<FriendshipRequestEntity?> UpdateFriendshipRequestAsync(FriendshipRequestEntity entity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<FriendshipRequestEntity?> FindFriendshipRequestByIdAsync(string requestId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<FriendshipRequestEntity?> DeleteFriendshipRequestByIdAsync(string requestId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<(IReadOnlyCollection<FriendSuggestionRepositoryDto> Items, int TotalCount)> GetFriendSuggestionsAsync(
        string userId, int depth, int page, int pageSize, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<(IReadOnlyCollection<FriendProfileRepositoryDto> Items, int TotalCount)> GetMutualFriendsAsync(
        string userIdA, string userIdB, int page, int pageSize, CancellationToken ct)
        => throw new NotImplementedException();
}
