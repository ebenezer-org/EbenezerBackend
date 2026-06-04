using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthCheckController : ControllerBase
    {
        private readonly ILogger<HealthCheckController> _logger;

        public HealthCheckController(ILogger<HealthCheckController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "HealthCheck")]
        public string Get()
        {
            return "Hello, World!";
        }
    }
}
