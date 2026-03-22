using DailyChallenges.DTOs;
using DailyChallenges.Models;
using System.Linq;

namespace DailyChallenges.Mapping
{
    public static class DtoMapper
    {
        public static SubmissionDto ToDto(Submission s)
        {
            return new SubmissionDto
            {
                Id = s.Id,
                GameId = s.GameId,
                Score = s.Score,
                Username = s.Username,
                ScreenshotUrl = s.ScreenshotData != null ? $"/api/submissions/{s.Id}/screenshot" : null,
                CreatedAt = s.CreatedAt
            };
        }

        public static GameDto ToDto(Game g, bool includeSubmissions = false)
        {
            return new GameDto
            {
                Id = g.Id,
                Name = g.Name,
                ImageUrl = g.ScreenshotData != null ? $"/api/games/{g.Id}/image" : null,
                Submissions = includeSubmissions && g.Submissions != null
                    ? g.Submissions.Select(ToDto).ToList()
                    : null
            };
        }
    }
}
