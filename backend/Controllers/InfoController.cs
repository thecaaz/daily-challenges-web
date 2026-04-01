using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text.Json;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public InfoController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        public IActionResult Get()
        {
            // Only source the version from CI-produced `release-info.json` placed into the backend content root.
            // If absent (local dev), return a sensible default of "dev".
            string version = "dev";
            string rawVersion = null;
            string commit = null;
            string timestamp = null;

            // If the pipeline wrote a release-info.json into the backend content root, prefer it.
            try
            {
                var releasePath = Path.Combine(_env.ContentRootPath, "release-info.json");
                if (System.IO.File.Exists(releasePath))
                {
                    var txt = System.IO.File.ReadAllText(releasePath);
                    using var doc = JsonDocument.Parse(txt);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("rawVersion", out var rv) && !string.IsNullOrWhiteSpace(rv.GetString()))
                    {
                        version = rv.GetString();
                    }
                    else if (root.TryGetProperty("version", out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
                    {
                        version = v.GetString();
                    }

                    if (root.TryGetProperty("commit", out var c)) commit = c.GetString();
                    if (root.TryGetProperty("timestamp", out var t)) timestamp = t.GetString();
                }
            }
            catch { /* non-fatal */ }
            // No other fallbacks: only the CI-provided `release-info.json` determines the version.

            string changelog = string.Empty;
            try
            {
                // Try repo-root CHANGELOG.md relative to content root (repo layout)
                var candidate = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "CHANGELOG.md"));
                if (System.IO.File.Exists(candidate))
                {
                    changelog = System.IO.File.ReadAllText(candidate);
                }
                else
                {
                    // fallback to content root
                    var alt = Path.Combine(_env.ContentRootPath, "CHANGELOG.md");
                    if (System.IO.File.Exists(alt)) changelog = System.IO.File.ReadAllText(alt);
                }
            }
            catch
            {
                changelog = string.Empty;
            }

            return Ok(new { version, rawVersion, commit, timestamp, changelog });
        }
    }
}
