using EbenezerBackend.Features.Categories.Data;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Features.Categories.Domain.Services;

namespace EbenezerBackend.Features.Categories;

public static class CategoriesDependencyInjection
{
    public static IServiceCollection AddCategoriesModule(this IServiceCollection services)
    {
        services
            .AddScoped<ICategoriesService, CategoriesService>()
            .AddScoped<ICategoriesRepository, CategoriesRepository>();

        return services;
    }
}
