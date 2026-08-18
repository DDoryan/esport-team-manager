using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Tests.Integration.Web;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("__tests/security-validation")]
public sealed class SecurityValidationController : ControllerBase
{
    [HttpPost]
    public IActionResult Validate(SecurityValidationRequest request)
    {
        return Ok();
    }
}

public sealed class SecurityValidationRequest
{
    [Required]
    public string? Value { get; set; }
}