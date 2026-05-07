using EbenezerBackend.Shared.Configurations;
using EbenezerBackend.Shared.Configurations.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class VariablesExtension
{
    public static IServiceCollection AddVariables(this IServiceCollection services)
    {
        services.AddSingleton<IVariables>(new Variables());
        
        return services;
    }
}