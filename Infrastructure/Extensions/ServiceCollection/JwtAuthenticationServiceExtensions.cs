using System.Text;
using EbenezerBackend.Shared.Configurations.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class JwtAuthenticationServiceExtensions
{
    public static IServiceCollection AddJwtAuthenticationService(this IServiceCollection services, IVariables variables)
    {
        if (string.IsNullOrEmpty(variables.JwtSecretKey))
        {
            throw new InvalidOperationException("Missing JWT configuration environment variables");
        }

        var key = Encoding.ASCII.GetBytes(variables.JwtSecretKey);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
            
                    ValidateIssuer = true,
                    ValidIssuer = variables.JwtIssuer, 
            
                    ValidateAudience = true,
                    ValidAudience = variables.JwtAudience,
            
                    ValidateLifetime = true,
                };
            });

        return services;
    }
}