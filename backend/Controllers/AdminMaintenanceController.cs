using DailyChallenges.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/admin/maintenance")]
[Authorize(Roles = "Admin")]
public sealed class AdminMaintenanceController(IScoreMaintenanceService scores) : ControllerBase
{
    /// <summary>
    /// Re-parses every stored score with the current parser. Dry run by default; pass dryRun=false to apply.
    /// Past finalized days are not re-finalized.
    /// </summary>
    [HttpPost("reparse-scores")]
    public async Task<IActionResult> ReparseScores([FromQuery] bool dryRun = true) =>
        Ok(await scores.ReparseScoresAsync(dryRun));
}
