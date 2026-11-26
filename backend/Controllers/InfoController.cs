using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class InfoController(IInfoService info) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await info.GetInfoAsync());
}
