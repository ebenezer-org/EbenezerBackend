using EbenezerBackend.Features.Comments.Data;
using EbenezerBackend.Features.Comments.Domain.Repositories;
using EbenezerBackend.Features.Comments.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Features.Comments;

public static class CommentsDependencyInjection
{
    public static IServiceCollection AddCommentsModule(this IServiceCollection services)
    {
        services
            .AddScoped<ICommentsService, CommentsService>()
            .AddScoped<ICommentsRepository, CommentsRepository>();

        return services;
    }
}
