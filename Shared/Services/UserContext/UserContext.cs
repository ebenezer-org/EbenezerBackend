using System;
using System.Security.Claims;
using EbenezerBackend.Shared.Exceptions;
using Microsoft.AspNetCore.Http;

namespace EbenezerBackend.Shared.Services.UserContext;
public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public string Id => GetClaim(ClaimTypes.NameIdentifier);
    public string UserName => GetClaim(ClaimTypes.Name);
    public string Email => GetClaim(ClaimTypes.Email);

    private string GetClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        
        if (string.IsNullOrEmpty(value))
            throw new UnauthorizedException();
            
        return value;
    }
}