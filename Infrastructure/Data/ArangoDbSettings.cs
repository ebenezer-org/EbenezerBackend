namespace EbenezerBackend.Infrastructure.Data
{
    public class ArangoDbSettings
    {
        public string Protocol { get; set; } = "http";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8529;
        public string DatabaseName { get; set; } = "EbenezerMultimodelDB";
        public string? User { get; set; }
        public string? Password { get; set; }
    }
}
