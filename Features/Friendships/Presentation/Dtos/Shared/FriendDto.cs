using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;

public record FriendDto(
    string Id,
    UserEssentialDto Friend,
    DateTime RequestedAt,
    DateTime AcceptedAt
    );