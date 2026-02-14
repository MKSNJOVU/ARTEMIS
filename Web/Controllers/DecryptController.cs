using Microsoft.AspNetCore.Mvc;

namespace MKSSecureShare.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecryptController : ControllerBase
{
    private readonly ILogger<DecryptController> _logger;

    public DecryptController(ILogger<DecryptController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{id}")]

    public async Task<IActionResult> Download(string id)
    {
        // TODO: Look up encrypted blob by ID
        // TODO: If not found, return NotFound()
        // TODO: Return encrypted file as download
    }
}