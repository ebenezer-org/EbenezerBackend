using EbenezerBackend.Features.Comments.Data.Models;
using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Domain.Repositories;
using EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EbenezerBackend.Features.Comments.Data;

public class CommentsRepository(IMongoDatabase database) : BaseRepository<CommentsModel>, ICommentsRepository
{
    private readonly IMongoCollection<CommentsModel> _comments =
        database.GetCollection<CommentsModel>(CollectionName);

    private readonly IMongoCollection<ProfileModel> _profiles =
        database.GetCollection<ProfileModel>(DbCollections.Users);

    public async Task<CommentRepositoryResponseDto> InsertCommentAsync(
        InsertCommentRequestDto request, CancellationToken ct)
    {
        var model = new CommentsModel
        {
            Content = request.Content,
            AuthorId = request.AuthorId,
            ParentId = request.ParentId,
            ParentType = request.ParentType,
            ReactedBy = [],
            CommentsCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _comments.InsertOneAsync(model, cancellationToken: ct);

        if (request.ParentType == CommentParentTypeEnum.Comment
            && ObjectId.TryParse(request.ParentId, out _))
        {
            var parentFilter = Builders<CommentsModel>.Filter.Eq(x => x.Id, request.ParentId);
            var parentUpdate = Builders<CommentsModel>.Update.Inc(x => x.CommentsCount, 1);
            await _comments.UpdateOneAsync(parentFilter, parentUpdate, cancellationToken: ct);
        }

        var author = await FindProfileAsync(request.AuthorId, ct);

        return new CommentRepositoryResponseDto(
            AuthorProfileModel: author,
            CommentModel: model,
            DirectCommentsCount: 0
        );
    }

    public async Task<(IReadOnlyCollection<CommentRepositoryResponseDto> Items, int TotalCount)>
        ListDirectCommentsAsync(
            string parentId,
            CommentParentTypeEnum parentType,
            int page,
            int pageSize,
            CancellationToken ct)
    {
        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.ParentId, parentId),
            Builders<CommentsModel>.Filter.Eq(x => x.ParentType, parentType),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var totalCount = (int)await _comments.CountDocumentsAsync(filter, cancellationToken: ct);

        var offset = Math.Max(0, (page - 1) * pageSize);

        var comments = await _comments
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Limit(pageSize)
            .ToListAsync(ct);

        if (comments.Count == 0)
            return ([], totalCount);

        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var profilesMap = await LoadProfilesMapAsync(authorIds, ct);

        var items = comments.Select(comment =>
        {
            profilesMap.TryGetValue(comment.AuthorId, out var profile);
            return new CommentRepositoryResponseDto(
                AuthorProfileModel: profile ?? new ProfileModel { Id = comment.AuthorId },
                CommentModel: comment,
                DirectCommentsCount: comment.CommentsCount
            );
        }).ToList();

        return (items, totalCount);
    }

    public async Task<CommentRepositoryResponseDto?> FindCommentByIdAsync(
        string commentId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(commentId, out _))
            return null;

        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.Id, commentId),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var comment = await _comments.Find(filter).FirstOrDefaultAsync(ct);
        if (comment is null)
            return null;

        var author = await FindProfileAsync(comment.AuthorId, ct);

        return new CommentRepositoryResponseDto(
            AuthorProfileModel: author,
            CommentModel: comment,
            DirectCommentsCount: comment.CommentsCount
        );
    }

    public async Task<CommentRepositoryResponseDto?> UpdateCommentAsync(
        string commentId,
        string ownerId,
        string content,
        CancellationToken ct)
    {
        if (!ObjectId.TryParse(commentId, out _))
            return null;

        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.Id, commentId),
            Builders<CommentsModel>.Filter.Eq(x => x.AuthorId, ownerId),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var update = Builders<CommentsModel>.Update
            .Set(x => x.Content, content)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<CommentsModel>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _comments.FindOneAndUpdateAsync(filter, update, options, ct);
        if (updated is null)
            return null;

        var author = await FindProfileAsync(updated.AuthorId, ct);

        return new CommentRepositoryResponseDto(
            AuthorProfileModel: author,
            CommentModel: updated,
            DirectCommentsCount: updated.CommentsCount
        );
    }

    public async Task<bool> DeleteCommentAsync(
        string commentId, string ownerId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(commentId, out _))
            return false;

        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.Id, commentId),
            Builders<CommentsModel>.Filter.Eq(x => x.AuthorId, ownerId),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var update = Builders<CommentsModel>.Update
            .Set(x => x.DeletedAt, DateTime.UtcNow);

        var result = await _comments.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task AddReactionAsync(string commentId, string reactorId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(commentId, out _))
            return;

        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.Id, commentId),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var update = Builders<CommentsModel>.Update.AddToSet(x => x.ReactedBy, reactorId);

        await _comments.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task RemoveReactionAsync(string commentId, string reactorId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(commentId, out _))
            return;

        var filter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(x => x.Id, commentId),
            Builders<CommentsModel>.Filter.Eq(x => x.DeletedAt, null)
        );

        var update = Builders<CommentsModel>.Update.Pull(x => x.ReactedBy, reactorId);

        await _comments.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private async Task<ProfileModel> FindProfileAsync(string userId, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(x => x.Id, userId);
        var profile = await _profiles.Find(filter).FirstOrDefaultAsync(ct);
        return profile ?? new ProfileModel { Id = userId };
    }

    private async Task<Dictionary<string, ProfileModel>> LoadProfilesMapAsync(
        IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        var filter = Builders<ProfileModel>.Filter.In(x => x.Id, ids);
        var profiles = await _profiles.Find(filter).ToListAsync(ct);
        return profiles.ToDictionary(p => p.Id!, p => p);
    }
}
