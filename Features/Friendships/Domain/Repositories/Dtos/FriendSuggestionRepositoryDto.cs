
namespace EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;

public record FriendSuggestionRepositoryDto(
    FriendProfileRepositoryDto FriendSuggestion,
    FriendProfileRepositoryDto OldestMutualFriend,
    int MutualFriendsCount
    );