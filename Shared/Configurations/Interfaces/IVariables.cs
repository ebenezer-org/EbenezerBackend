namespace EbenezerBackend.Shared.Configurations.Interfaces;

public interface IVariables
{
    public string JwtSecretKey { get; }
    public string JwtIssuer { get; }
    public string JwtAudience { get; }
    public int JwtExpiryInMinutes { get; }
}