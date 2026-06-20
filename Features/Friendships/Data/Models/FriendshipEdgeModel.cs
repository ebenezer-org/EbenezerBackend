using System;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Friendships.Data.Models;

[CollectionName(DbEdges.Friendships)]
public class FriendshipEdgeModel(string fromUserName, string toUserName, string fromName, string toName, DateTime requestedAt, DateTime? acceptedAt)
    : IBaseModel<FriendshipEdgeModel, FriendshipRequestEntity>, INeo4JModel<FriendshipEdgeModel>
{
    public string? Id { get; set; }
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;

    public string FromUserName { get; } = fromUserName;
    public string FromName { get; } = fromName;
    public string ToUserName { get; } = toUserName;
    public string ToName { get; } = toName;
    public DateTime RequestedAt { get; } = requestedAt;
    public DateTime? AcceptedAt { get; } = acceptedAt;

    public FriendshipRequestEntity ToEntity()
    {
        return new FriendshipRequestEntity(
            id: Id!,
            requesterId: FromId,
            requesterUserName: FromUserName,
            requesterName: FromName,
            targetId: ToId,
            targetUserName: ToUserName,
            targetName: ToName,
            requestedAt: RequestedAt,
            acceptedAt: AcceptedAt
        );
    }

    public static FriendshipEdgeModel FromEntity(FriendshipRequestEntity entity)
    {
        return new FriendshipEdgeModel(
            fromUserName: entity.RequesterUserName,
            fromName: entity.RequesterName,
            toUserName: entity.TargetUserName,
            toName: entity.TargetName,
            requestedAt: entity.RequestedAt,
            acceptedAt: entity.AcceptedAt
            )
        {
            Id = entity.Id,
            FromId = entity.RequesterId,
            ToId = entity.TargetId
        };
    }

    public static FriendshipEdgeModel FromRecord(IRecord record)
    {
        return new FriendshipEdgeModel(
            fromUserName: record["fromUserName"].As<string>(),
            toUserName: record["toUserName"].As<string>(),
            fromName: record["fromName"].As<string>(),
            toName: record["toName"].As<string>(),
            requestedAt: Neo4JValueConverter.ToDateTime(record["requestedAt"]),
            acceptedAt: Neo4JValueConverter.ToNullableDateTime(record["acceptedAt"]))
        {
            Id = record["id"].As<string>(),
            FromId = record["fromId"].As<string>(),
            ToId = record["toId"].As<string>()
        };
    }
}
