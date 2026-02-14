using Microsoft.AspNetCore.Mvc;

namespace MKSSecureShare.Web.Controllers;

public class DecryptController : ControllerBase
{
    private readonly ILogger<DecryptController> _logger;

    public DecryptController(ILogger<DecryptController> logger)
    {
        _logger = logger;
    }
}