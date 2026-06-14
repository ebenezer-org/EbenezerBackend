using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;

public record FriendSuggestionDto(
    string Id,
    string Name,
    string UserName,
    int MutualFriendsCount,
    UserEssentialDto OldestMutualFriend
    
    ) : UserEssentialDto(Id, Name, UserName);