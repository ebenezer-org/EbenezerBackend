using EbenezerBackend.Features.Retrospective.Data;
using EbenezerBackend.Features.Retrospective.Domain.Repositories;
using EbenezerBackend.Features.Retrospective.Domain.Services;

namespace EbenezerBackend.Features.Retrospective;

public static class RetrospectiveDependencyInjection
{
    public static IServiceCollection AddRetrospectiveModule(this IServiceCollection services)
    {
        services
            .AddScoped<IRetrospectiveService, RetrospectiveService>()
            .AddScoped<IRetrospectiveRepository, RetrospectiveRepository>();
        
        return services;
    }
}