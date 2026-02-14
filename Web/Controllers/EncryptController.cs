using Microsoft.AspNetCore.Mvc;
using MKSSecureShare.Web;
using System.Diagnostics;

namespace MKSSecureShare.Web.Controllers;

public class EncryptController : ControllerBase
{
    private readonly ILogger<EncryptController> _logger;

    public EncryptController(ILogger<EncryptController> logger)
    {
        _logger = logger;
    }

}