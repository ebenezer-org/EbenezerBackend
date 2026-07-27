using EbenezerBackend.Features.Comments.Data.Models;
using EbenezerBackend.Features.Profile.Data.Models;

namespace EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;

public record CommentRepositoryResponseDto(
    ProfileModel AuthorProfileModel,
    CommentsModel CommentModel,
    int DirectCommentsCount
);
