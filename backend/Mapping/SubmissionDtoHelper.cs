using System;
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
    }
}
