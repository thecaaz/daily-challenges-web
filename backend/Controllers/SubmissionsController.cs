using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SubmissionsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet("game/{gameId}")]
        public async Task<IActionResult> GetByGame(int gameId)
        {
            var subs = await _db.Submissions.Where(s => s.GameId == gameId).OrderByDescending(s => s.CreatedAt).ToListAsync();
            return Ok(subs);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] int gameId, [FromForm] string score, [FromForm] string? username, [FromForm] IFormFile? screenshot)
        {
            var game = await _db.Games.FindAsync(gameId);
            if (game == null) return BadRequest("invalid gameId");
            if (string.IsNullOrWhiteSpace(score)) return BadRequest("score is required");

            var submission = new Submission { GameId = gameId, Score = score, Username = username };

            if (screenshot != null && screenshot.Length > 0)
            {
                var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsPath);
                var filename = Path.GetRandomFileName() + Path.GetExtension(screenshot.FileName);
                var filePath = Path.Combine(uploadsPath, filename);
                using var stream = System.IO.File.Create(filePath);
                await screenshot.CopyToAsync(stream);
                submission.ScreenshotUrl = Path.Combine("/uploads", filename).Replace("\\", "/");
            }

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetByGame), new { gameId = gameId }, submission);
        }
    }
}
