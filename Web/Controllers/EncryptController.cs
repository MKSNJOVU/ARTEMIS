using Microsoft.AspNetCore.Mvc;
using MKSSecureShare.Web;
using System.Diagnostics;

namespace MKSSecureShare.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EncryptController : ControllerBase
{
    private readonly ILogger<EncryptController> _logger;

    public EncryptController(ILogger<EncryptController> logger)
    {
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile encryptedFile)
    {
        // TODO: Validate file exists and size limits
        // TODO: Generate unique ID for this upload
        // TODO: Save encrypted blob to storage
        // TODO: Return the share ID or URL

        return Ok(new { message = "response" });
    }

}