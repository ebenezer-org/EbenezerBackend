using EbenezerBackend.Features.Auth.Data;
using EbenezerBackend.Features.Auth.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class IdentityServiceExtensions
{
    /// <summary>
    /// Número de iterações do PBKDF2 usado pelo PasswordHasher padrão do Identity.
    /// O default do .NET é 100.000 iterações (PBKDF2-HMAC-SHA512), o que custa dezenas
    /// de milissegundos de CPU por register/login.
    ///
    /// Como todos os cenários de carga autenticam antes de exercitar o banco, esse custo
    /// entra no caminho crítico da suíte inteira e é CPU puramente da aplicação — ele
    /// não diz nada sobre MongoDB vs ArangoDB e mascara a comparação entre as abordagens.
    /// Reduzir as iterações remove esse ruído da medição.
    ///
    /// ATENÇÃO: este valor enfraquece deliberadamente a proteção das senhas e existe
    /// apenas para viabilizar o benchmark. Antes de qualquer uso real da API, remover
    /// esta configuração para voltar ao default de 100.000.
    /// </summary>
    private const int BenchmarkPasswordHasherIterations = 1_000;

    public static IServiceCollection AddCustomIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<AuthUserEntity>()
            .AddUserStore<AuthRepository>()
            .AddDefaultTokenProviders();

        services.Configure<PasswordHasherOptions>(options =>
            options.IterationCount = BenchmarkPasswordHasherIterations);

        return services;
    }
}