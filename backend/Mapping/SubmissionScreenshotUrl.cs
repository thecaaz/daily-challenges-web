namespace DailyChallenges.Mapping;

/// <summary>Plain (unsigned) URL of a submission's screenshot endpoint.</summary>
public static class SubmissionScreenshotUrl
{
    public static string Of(int submissionId) => $"/api/submissions/{submissionId}/screenshot";

    /// <summary>Null when the submission has no stored screenshot.</summary>
    public static string? For(int submissionId, string? screenshotContentType) =>
        string.IsNullOrEmpty(screenshotContentType) ? null : Of(submissionId);
}
