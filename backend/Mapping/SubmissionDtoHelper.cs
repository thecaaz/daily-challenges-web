using DailyChallenges.DTOs;
using DailyChallenges.Models;

namespace DailyChallenges.Mapping
{
    public static class SubmissionDtoHelper
    {
        public static SubmissionDto ToDtoWithScoringDay(Submission s, DateTime? scoringDay = null, bool isDayWinner = false)
        {
            var dto = DtoMapper.ToDto(s);
            if (scoringDay.HasValue)
                dto.ScoringDay = Services.ScoringDayHelper.FormatScoringDay(scoringDay.Value);
            else if (s.ScoringDay != DateTime.MinValue)
                dto.ScoringDay = Services.ScoringDayHelper.FormatScoringDay(s.ScoringDay.Date);

            dto.IsDayWinner = isDayWinner;
            return dto;
        }

        public static SubmissionPageDto ToPageDto(List<SubmissionDto> items, int page, int pageSize, bool hasSubmittedForLatest, int totalCount)
        {
            var result = new SubmissionPageDto
            {
                Items = items,
                HasSubmittedForLatest = hasSubmittedForLatest,
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
