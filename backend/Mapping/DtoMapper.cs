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
                UserId = s.UserId,
                Score = s.Score,
                ScoreValue = s.ScoreValue,
                Username = s.Username,
                ScreenshotUrl = s.ScreenshotData != null ? $"/api/submissions/{s.Id}/screenshot" : null,
                CreatedAt = s.CreatedAt
            };
        }

        public static GameDto ToDto(Game g, bool includeSubmissions = false)
        {
            var dto = new GameDto
            {
                Id = g.Id,
                Name = g.Name,
                Url = g.Url,
                ImageUrl = g.ScreenshotData != null ? $"/api/games/{g.Id}/image" : null,
                // ResetTime is exported as HH:mm and timezone id as provided
                ResetTime = g.ResetTime.ToString(@"hh\:mm"),
                ResetTimezoneId = g.ResetTimezoneId,
                Submissions = includeSubmissions && g.Submissions != null
                    ? g.Submissions.Select(ToDto).ToList()
                    : null
            };

            try
            {
                // Compute current scoring day on the server side so frontend can rely on it
                dto.CurrentScoringDay = Services.ScoringDayHelper.GetCurrentScoringDay(g.ResetTime, g.ResetTimezoneId).ToString("yyyy-MM-dd");
            }
            catch
            {
                dto.CurrentScoringDay = null;
            }

            return dto;
        }
    }
}
