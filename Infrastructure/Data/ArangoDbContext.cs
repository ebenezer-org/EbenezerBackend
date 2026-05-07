using System;
using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;

namespace EbenezerBackend.Infrastructure.Data
{
    public class ArangoDbContext : IDisposable
    {
        private HttpApiTransport _transport = null!;
        public IArangoDBClient Client { get; private set; } = null!;
        private readonly ArangoDbSettings _settings;

        public ArangoDbContext(ArangoDbSettings settings)
        {
            _settings = settings;
            Connect(settings.DatabaseName);
        }

        public void Connect(string dbName)
        {
            _transport?.Dispose();
            _transport = HttpApiTransport.UsingBasicAuth(
                new Uri($"{_settings.Protocol}://{_settings.Host}:{_settings.Port}"),
                dbName,
                _settings.User,
                _settings.Password
            );
            Client = new ArangoDBClient(_transport);
        }

        public void Dispose() => _transport?.Dispose();
    }
}
