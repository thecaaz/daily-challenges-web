using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyChallenges.Controllers;

[ApiController]
[Route("api/admin/maintenance")]
[Authorize(Roles = AuthRoles.Admin)]
public sealed class AdminMaintenanceController(IScoreMaintenanceService scores) : ControllerBase
{
    /// <summary>
    /// Re-parses every stored score with the current parser. Dry run by default; pass dryRun=false to apply.
    /// Past finalized days are not re-finalized.
    /// </summary>
    [HttpPost("reparse-scores")]
    public async Task<IActionResult> ReparseScores([FromQuery] bool dryRun = true, CancellationToken ct = default) =>
        Ok(dryRun ? await scores.PreviewReparseScoresAsync(ct) : await scores.ReparseScoresAsync(ct));
}
