using Microsoft.AspNetCore.Mvc;

namespace HerreraSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    // Ejercicio 1: GET /api/test/fail lanza una excepción intencional
    [HttpGet("fail")]
    public IActionResult Fail()
    {
        throw new InvalidOperationException("Falla intencional para probar los logs.");
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("pong");
}