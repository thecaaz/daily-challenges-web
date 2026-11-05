namespace DailyChallenges.DTOs
{
    /// <summary>Result of POST /api/admin/maintenance/reparse-scores.</summary>
    public class ReparseScoresReportDto
    {
        /// <summary>True when nothing was written.</summary>
        public bool DryRun { get; set; }
        /// <summary>Submissions examined.</summary>
        public int Total { get; set; }
        /// <summary>Submissions whose ScoreValue differs under the current parser (includes the two counts below).</summary>
        public int Changed { get; set; }
        /// <summary>Had a value, now unparseable: becomes unranked.</summary>
        public int BecameNull { get; set; }
        /// <summary>Had no value, now parses: becomes ranked.</summary>
        public int BecameNumeric { get; set; }
        /// <summary>Up to <see cref="SampleLimit"/> changed rows.</summary>
        public List<ReparseScoreSampleDto> Samples { get; set; } = new();
        public int SampleLimit { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ReparseScoreSampleDto
    {
        public int Id { get; set; }
        public string Score { get; set; } = string.Empty;
        public double? OldValue { get; set; }
        public double? NewValue { get; set; }
    }
}
