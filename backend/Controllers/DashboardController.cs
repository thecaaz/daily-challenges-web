using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboard;

        public DashboardController(IDashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue) return Forbid();

            var data = await _dashboard.GetDashboardDataAsync(userId.Value);
            return Ok(data);
        }
    }
}
