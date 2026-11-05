using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping
{
    public static class SubmissionDtoHelper
    {
        public static SubmissionDto ToDtoWithScoringDay(Submission s, DateTime? scoringDay = null, bool isDayWinner = false)
        {
            var dto = DtoMapper.ToDto(s);
            if (scoringDay.HasValue)
                dto.ScoringDay = ScoringDayHelper.FormatScoringDay(scoringDay.Value);
            else if (s.ScoringDay != DateTime.MinValue)
                dto.ScoringDay = ScoringDayHelper.FormatScoringDay(s.ScoringDay.Date);

            dto.IsDayWinner = isDayWinner;
            return dto;
        }

        /// <summary>
        /// Hides everything that reveals the score: Score, ScoreValue, Rank, ScreenshotUrl and the winner flag.
        /// Id, GameId, UserId, Username, CreatedAt and ScoringDay stay.
        /// </summary>
        public static SubmissionDto Mask(SubmissionDto dto)
        {
            dto.Score = null;
            dto.ScoreValue = null;
            dto.Rank = null;
            dto.ScreenshotUrl = null;
            dto.IsDayWinner = false;
            return dto;
        }

        /// <summary>
        /// Replaces the plain screenshot URL with a signed one when <paramref name="sign"/> is true
        /// (current-day submissions the caller may see). Does nothing when there is no screenshot.
        /// </summary>
        public static SubmissionDto SignScreenshot(SubmissionDto dto, IScreenshotUrlSigner signer, bool sign)
        {
            if (dto.ScreenshotUrl != null) dto.ScreenshotUrl = signer.Url(dto.Id, sign);
            return dto;
        }

        public static SubmissionPageDto ToPageDto(List<SubmissionDto> items, int page, int pageSize, bool hasSubmittedForLatest, int totalCount, bool scoresHidden = false)
        {
            var result = new SubmissionPageDto
            {
                Items = items,
                HasSubmittedForLatest = hasSubmittedForLatest,
                ScoresHidden = scoresHidden,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            result.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            result.HasMore = page < result.TotalPages;
            return result;
        }

        public static TodaySubmittersDto ToTodaySubmittersDto(List<string> usernames)
        {
            return new TodaySubmittersDto { Count = usernames.Count, Usernames = usernames };
        }
    }
}
