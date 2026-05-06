using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.User.Presentation;

[Route("[controller]")]
public class UserController : ControllerBase
{
    [HttpGet("{username}")]
    public IActionResult GetUser([FromRoute] string username)
    {
        return Ok();
    }
}