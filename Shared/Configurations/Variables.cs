using System;
using EbenezerBackend.Shared.Configurations.Interfaces;

namespace EbenezerBackend.Shared.Configurations;

public class Variables : IVariables
{
    public string JwtSecretKey { get; } = GetEnvironmentVariable("JWT_SECRET_KEY"); 
    public string JwtIssuer { get; }  = GetEnvironmentVariable("JWT_ISSUER");
    public string JwtAudience { get; }  = GetEnvironmentVariable("JWT_AUDIENCE");
    public int JwtExpiryInMinutes { get; } = int.Parse(GetEnvironmentVariable("JWT_EXPIRY_IN_MINUTES"));

    private static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName)!;
    }
}