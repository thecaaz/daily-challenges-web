using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase
    {
        private readonly IInfoService _info;

        public InfoController(IInfoService info)
        {
            _info = info;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var release = await _info.ReadReleaseInfoAsync();
            var changelog = await _info.FetchChangelogAsync();

            return Ok(new
            {
                version = release?.Version ?? "dev",
                rawVersion = release?.RawVersion,
                commit = release?.Commit,
                timestamp = release?.Timestamp,
                changelog
            });
        }
    }
}
