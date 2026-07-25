using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using EbenezerBackend.Features.Comments.Data.Models;
using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Domain.Repositories;
using EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Comments.Data;

public class CommentsRepository(IArangoDBClient db) : BaseRepository<CommentsModel>, ICommentsRepository
{
    public async Task<CommentRepositoryResponseDto> InsertCommentAsync(InsertCommentRequestDto request, CancellationToken ct)
    {
        var query = $@"
            LET author = DOCUMENT(@authorId)
            FILTER author != null

            LET parent = DOCUMENT(@parentId)
            FILTER parent != null

            INSERT @comment INTO {CollectionName}
            LET newComment = NEW

            INSERT {{
                _from: newComment._id,
                _to: parent._id,
                CreatedAt: DATE_ISO8601(DATE_NOW())
            }} INTO {ArangoDbEdges.CommentedOn}

            RETURN {{
               AuthorProfileModel: author,
               CommentModel: newComment,
               DirectCommentsCount: 0
           }}
        ";

        var commentDoc = new CommentsModel()
        {
            Key = Guid.NewGuid().ToString(),
            Content = request.Content,
            AuthorId = ArangoDbUtils.BuildArangoDbId(request.AuthorId, ArangoDbCollections.Users)!,
            ReactedBy = [],
            CommentsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        
        var parentCollection = request.ParentType is CommentParentTypeEnum.Prayer ? ArangoDbCollections.Prayers : ArangoDbCollections.Comments;

        var bindVars = new Dictionary<string, object>
        {
            { "authorId", commentDoc.AuthorId },
            { "parentId", ArangoDbUtils.BuildArangoDbId(request.ParentId, parentCollection)! },
            { "comment", commentDoc }
        };

        var response = await db.Cursor.PostCursorAsync<CommentRepositoryResponseDto>(query, bindVars, token: ct);
        var result = response.Result.FirstOrDefault();

        return result ?? throw new KeyNotFoundException("Autor ou post de destino não encontrado.");
    }

    public async Task<(IReadOnlyCollection<CommentRepositoryResponseDto> Items, int TotalCount)> ListDirectCommentsAsync(
        string parentId,
        CommentParentTypeEnum parentType,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = $@"
            LET parent = DOCUMENT(@parentId)
            FILTER parent != null

            FOR comment IN 1..1 INBOUND parent._id {ArangoDbEdges.CommentedOn}
                FILTER IS_SAME_COLLECTION({CollectionName}, comment)

                LET author = DOCUMENT({ArangoDbCollections.Users}, comment.AuthorId)

                LET directCommentsCount = LENGTH(
                    FOR child IN 1..1 INBOUND comment._id {ArangoDbEdges.CommentedOn}
                        FILTER IS_SAME_COLLECTION({CollectionName}, child)
                        RETURN 1
                )

                SORT comment.CreatedAt DESC
                LIMIT @offset, @pageSize

                RETURN {{
                    AuthorProfileModel: author,
                    CommentModel: comment,
                    DirectCommentsCount: directCommentsCount
                }}
        ";

        var offset = Math.Max(0, (page - 1) * pageSize);

        var parentCollection = parentType is CommentParentTypeEnum.Prayer
            ? ArangoDbCollections.Prayers
            : ArangoDbCollections.Comments;

        var bindVars = new Dictionary<string, object>
        {
            { "parentId", ArangoDbUtils.BuildArangoDbId(parentId, parentCollection)! },
            { "offset", offset },
            { "pageSize", pageSize }
        };

        var postCursorBody = new PostCursorBody
        {
            Query = query,
            BindVars = bindVars,
            Options = new PostCursorOptions { FullCount = true }
        };

        var response = await db.Cursor.PostCursorAsync<CommentRepositoryResponseDto>(postCursorBody, token: ct);
        var totalCount = (int)(response.Extra?.Stats?.FullCount ?? 0);

        return (response.Result.ToList(), totalCount);
    }

    public async Task<CommentRepositoryResponseDto?> FindCommentByIdAsync(string commentId, CancellationToken ct)
    {
        var query = $@"
            LET comment = DOCUMENT(@commentId)
            FILTER comment != null

            LET author = FIRST(
                FOR u IN 1..1 INBOUND comment._id {ArangoDbEdges.CommentedOn}
                    FILTER IS_SAME_COLLECTION({ArangoDbCollections.Users}, u)
                    LIMIT 1
                    RETURN u
            )

            LET directCommentsCount = LENGTH(
                FOR child IN 1..1 INBOUND comment._id {ArangoDbEdges.CommentedOn}
                    FILTER IS_SAME_COLLECTION({CollectionName}, child)
                    RETURN 1
            )

            RETURN {{
                AuthorProfileModel: author,
                CommentModel: comment,
                DirectCommentsCount: directCommentsCount
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "commentId", ArangoDbUtils.BuildArangoDbId(commentId, ArangoDbCollections.Comments)! }
        };

        var response = await db.Cursor.PostCursorAsync<CommentRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
    }

    public async Task<CommentRepositoryResponseDto?> UpdateCommentAsync(
        string commentId,
        string ownerId,
        string content,
        CancellationToken ct)
    {
        var query = $@"
            LET commentToUpdate = DOCUMENT(@commentId)

            FILTER commentToUpdate != null
            FILTER commentToUpdate.AuthorId == @ownerId

            LET author = DOCUMENT(commentToUpdate.AuthorId)

            UPDATE commentToUpdate WITH {{
                Content: @content,
                UpdatedAt: DATE_ISO8601(DATE_NOW())
            }} IN {CollectionName}
            LET updatedComment = NEW

            RETURN {{
                AuthorProfileModel: author,
                CommentModel: updatedComment,
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "commentId", ArangoDbUtils.BuildArangoDbId(commentId, ArangoDbCollections.Comments)! },
            { "ownerId", ArangoDbUtils.BuildArangoDbId(ownerId, ArangoDbCollections.Users)! },
            { "content", content }
        };

        var response = await db.Cursor.PostCursorAsync<CommentRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
    }

    public async Task<bool> DeleteCommentAsync(string commentId, string ownerId, CancellationToken ct)
    {
        var query = $@"
            LET commentToDelete = DOCUMENT(@commentId)

            FILTER commentToDelete != null
            FILTER commentToDelete.AuthorId == @ownerId

            LET removedEdges = (
                FOR edge IN {ArangoDbEdges.CommentedOn}
                    FILTER edge._from == commentToDelete._id || edge._to == commentToDelete._id
                    REMOVE edge IN {ArangoDbEdges.CommentedOn}
                    RETURN OLD
            )

            LET removedReactions = (
                FOR edge IN {ArangoDbEdges.ReactedBy}
                    FILTER edge._to == commentToDelete._id
                    REMOVE edge IN {ArangoDbEdges.ReactedBy}
                    RETURN OLD
            )

            REMOVE commentToDelete IN {CollectionName}
            RETURN 1
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "commentId", ArangoDbUtils.BuildArangoDbId(commentId, ArangoDbCollections.Comments)! },
            { "ownerId", ArangoDbUtils.BuildArangoDbId(ownerId, ArangoDbCollections.Users)! }
        };

        var response = await db.Cursor.PostCursorAsync<int>(query, bindVars, token: ct);

        return response.Result.Any();
    }

    public async Task AddReactionAsync(string commentId, string reactorId, CancellationToken ct)
    {
        const string query = $@"
            LET user = DOCUMENT(@reactorId)
            FILTER user != null

            LET comment = DOCUMENT(@commentId)
            FILTER comment != null

            LET existingReaction = FIRST(
                FOR edge IN {ArangoDbEdges.ReactedBy}
                    FILTER edge._from == user._id && edge._to == comment._id
                    LIMIT 1
                    RETURN edge
            )

            LET newReaction = (
                FOR i IN existingReaction == null ? [1] : []
                    INSERT {{
                        _from: user._id,
                        _to: comment._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {ArangoDbEdges.ReactedBy}
                    RETURN NEW
            )

            RETURN true
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "reactorId", ArangoDbUtils.BuildArangoDbId(reactorId, ArangoDbCollections.Users)! },
            { "commentId", ArangoDbUtils.BuildArangoDbId(commentId, ArangoDbCollections.Comments)! }
        };

        await db.Cursor.PostCursorAsync(query, bindVars, token: ct);
    }

    public async Task RemoveReactionAsync(string commentId, string reactorId, CancellationToken ct)
    {
        var query = $@"
            LET user = DOCUMENT(@reactorId)
            FILTER user != null

            LET comment = DOCUMENT(@commentId)
            FILTER comment != null

            FOR edge IN {ArangoDbEdges.ReactedBy}
                FILTER edge._from == user._id && edge._to == comment._id
                REMOVE edge IN {ArangoDbEdges.ReactedBy}

            RETURN true
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "reactorId", ArangoDbUtils.BuildArangoDbId(reactorId, ArangoDbCollections.Users)! },
            { "commentId", ArangoDbUtils.BuildArangoDbId(commentId, ArangoDbCollections.Comments)! }
        };

        await db.Cursor.PostCursorAsync(query, bindVars, token: ct);
    }
}
