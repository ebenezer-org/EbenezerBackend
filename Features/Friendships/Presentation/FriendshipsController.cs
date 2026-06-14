using System;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Friendships.Domain.Services;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendships;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendSuggestions;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetMyFriendshipRequests;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.SendFriendRequest;
using EbenezerBackend.Shared.Web.Dtos.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Friendships.Presentation;

[ApiController]
[Route("[controller]")]
public class FriendshipsController(IFriendshipsService service) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMyFriendships([FromQuery] GetFriendshipsRequestDto request, CancellationToken ct)
    {
        var result = await service.GetFriendships(request, ct);
        
        return Ok(result);
    }
    
    [Authorize]
    [HttpGet("requests/to-me")]
    public async Task<IActionResult> GetMyFriendshipRequests([FromQuery] GetPendingFriendshipRequestsToMeRequestDto requestToMe, CancellationToken ct) {
        var result = await service.GetPendingFriendshipRequestsAsync(requestToMe, ct);
        
        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestRequestDto request, CancellationToken ct)
    {
        var result = await service.SendFriendRequestAsync(request, ct);
        
        return Created("/friendships/mine", result);
    }
    
    [Authorize]
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetFriendSuggestions([FromQuery] GetFriendSuggestionsRequestDto request, CancellationToken ct)
    {
        var result = await service.GetFriendSuggestionsAsync(request, ct);
        return Ok(result);
    }
    
    [Authorize]
    [HttpGet("my-mutual-friends-with/{otherUserId}")]
    public async Task<IActionResult> GetMyMutualFriendsWith([FromRoute] string otherUserId, [FromQuery] PaginationRequestDto pagination, CancellationToken ct)
    {
        var result = await service.GetUsersMutualFriendsWithAsync(otherUserId, pagination, ct);
        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> AcceptFriendRequest([FromRoute] string id, CancellationToken ct)
    {
        var result = await service.AcceptFriendRequestAsync(id, ct);
        
        return Ok(result);
    }
    
    [Authorize]
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> DeclineFriendRequest([FromRoute] string id, CancellationToken ct)
    {
        await service.DeclineFriendRequestAsync(id, ct);
        
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id}/cancel-request")]
    public async Task<IActionResult> CancelFriendRequest([FromRoute] string id, CancellationToken ct)
    {
        await service.CancelFriendRequestAsync(id, ct);

        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{friendId}/remove")]
    public async Task<IActionResult> RemoveFriend([FromRoute] string friendId, CancellationToken ct)
    {
        await service.RemoveFriendAsync(friendId, ct);

        return NoContent();
    }
}