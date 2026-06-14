namespace EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;

public record FriendProfileRepositoryDto(
    string Id,
    string FullName,
    string UserName
    );