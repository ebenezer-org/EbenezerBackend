using System;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;

public record FriendshipRequestDto(
    string Id,
    string RequesterId,
    string RequesterName,
    string TargetId,
    string TargetName,
    DateTime RequestedAt,
    DateTime? AcceptedAt
    );