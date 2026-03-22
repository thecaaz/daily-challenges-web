using Microsoft.AspNetCore.Mvc;
using DailyChallenges.Data;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using DailyChallenges.Repositories;

namespace DailyChallenges.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IGameRepository _games;
        private readonly IWebHostEnvironment _env;

        public GamesController(IGameRepository games, IWebHostEnvironment env)
        {
            _games = games;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var games = await _games.GetAllAsync();
            var dtos = games.Select(g => DailyChallenges.Mapping.DtoMapper.ToDto(g)).ToList();
            return Ok(dtos);
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

            var created = await _games.CreateAsync(game);
            var dto = DailyChallenges.Mapping.DtoMapper.ToDto(created);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, dto);
        }
    }
}
