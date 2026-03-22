using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public GamesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var games = await _db.Games.ToListAsync();
            return Ok(games);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile? image)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("name is required");

            var game = new Game { Name = name };

            if (image != null && image.Length > 0)
            {
                var imagesPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "games");
                Directory.CreateDirectory(imagesPath);
                var filename = Path.GetRandomFileName() + Path.GetExtension(image.FileName);
                var filePath = Path.Combine(imagesPath, filename);
                using var stream = System.IO.File.Create(filePath);
                await image.CopyToAsync(stream);
                game.ImageUrl = Path.Combine("/images/games", filename).Replace("\\", "/");
            }

            _db.Games.Add(game);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = game.Id }, game);
        }
    }
}
