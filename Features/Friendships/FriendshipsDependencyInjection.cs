using EbenezerBackend.Features.Friendships.Data;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Features.Friendships;

public static class FriendshipsDependencyInjection
{
    public static IServiceCollection AddFriendshipsModule(this IServiceCollection services) 
    {
        services.AddScoped<IFriendshipsRepository, FriendshipsRepository>();
        services.AddScoped<IFriendshipsService, FriendshipsService>();
        
        return services;
    }
}