using System;
using EbenezerBackend.Features.Friendships.Domain.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Entities;

public class FriendshipRequestEntity(string id, string requesterId, string requesterUserName, string requesterName, string targetId, string targetUserName, string targetName, DateTime requestedAt, DateTime? acceptedAt)
{
    public string Id { get; } = id;
    public string RequesterId { get; } = requesterId;
    public string RequesterUserName { get; } = requesterUserName;
    public string RequesterName { get; } = requesterName;
    public string TargetId { get; } = targetId;
    public string TargetUserName { get; } = targetUserName;
    public string TargetName { get; } = targetName;
    public DateTime RequestedAt { get; } = requestedAt;
    public DateTime? AcceptedAt { get; private set; } = acceptedAt;
    
    public bool IsAccepted => AcceptedAt.HasValue;

    public string GetFriendName(string userId)
    {
        if (!ContainsUser(userId))
        {        
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }                    
        
        return userId == RequesterId ? TargetName : RequesterName;
    }
    
    public string GetFriendUserName(string userId)
    {
        if (!ContainsUser(userId))
        {        
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }                    
        
        return userId == RequesterId ? TargetUserName : RequesterUserName;
    }
    
    public string GetFriendId(string userId)
    {
        if (!ContainsUser(userId))
        {        
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }                    
        
        return userId == RequesterId ? TargetId : RequesterId;
    }
    
    public void AcceptRequest(string userId)
    {
        if (IsAccepted)
        {
            return;
        }
        
        if (!IsTarget(userId)) 
        {
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }
        
        AcceptedAt = DateTime.UtcNow;
    }

    public bool IsRequester(string userId)
    {
        return RequesterId == userId;
    }
    
    public bool IsTarget(string userId)
    {
        return TargetId == userId;
    }

    public bool ContainsUser(string userId)
    {
        return IsRequester(userId) || IsTarget(userId);
    }
}