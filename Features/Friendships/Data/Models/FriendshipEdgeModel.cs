using System;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using Newtonsoft.Json;

namespace EbenezerBackend.Features.Friendships.Data.Models;

[CollectionName(DbEdges.Friendships)]
public class FriendshipEdgeModel(string fromUserName, string toUserName, string fromName, string toName, DateTime requestedAt, DateTime? acceptedAt) : IBaseModel<FriendshipEdgeModel, FriendshipRequestEntity>
{
    public string? Key { get; set; }

    public string Id => Key ?? string.Empty;

    [JsonProperty("_from")]
    public string FromId { get; set; } = string.Empty;
    
    [JsonIgnore]
    public string FromKey 
    { 
        get => FromId.Contains('/') ? FromId.Split('/')[1] : FromId;
        set => FromId = $"{DbCollections.Users}/{value}";
    }

    [JsonProperty("_to")]
    public string ToId { get; set; } = string.Empty;

    [JsonIgnore]
    public string ToKey 
    {
        get => ToId.Contains('/') ? ToId.Split('/')[1] : ToId;
        set => ToId = $"{DbCollections.Users}/{value}";
    }
    
    public string FromUserName { get; } = fromUserName;
    public string FromName { get; } = fromName;
    public string ToUserName { get; } = toUserName;
    public string ToName { get; } = toName;
    public DateTime RequestedAt { get; } = requestedAt;
    public DateTime? AcceptedAt { get; } = acceptedAt;
    
    public FriendshipRequestEntity ToEntity()
    {
        return new FriendshipRequestEntity(
            id: Key!,
            requesterId: FromKey,
            requesterUserName: FromUserName,
            requesterName: FromName,
            targetId: ToKey,
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
            Key = entity.Id,
            FromKey = entity.RequesterId,
            ToKey = entity.TargetId
        };
    }
}