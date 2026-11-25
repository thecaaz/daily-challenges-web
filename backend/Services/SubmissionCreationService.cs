using DailyChallenges.Achievements;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DailyChallenges.Services;

public sealed class SubmissionCreationService(
    SubmissionDraftBuilder drafts,
    ISubmissionRepository submissions,
    IXpService xp,
    IAchievementService achievements,
    ITransactionRunner transactions,
    IScreenshotUrlSigner screenshotSigner,
    ILogger<SubmissionCreationService> logger) : ISubmissionCreationService
{
    public async Task<(SubmissionDto Dto, int XpGain)> CreateAsync(int gameId, string score, string? username, IFormFile? screenshot, ClaimsPrincipal user)
    {
        var draft = await drafts.BuildAsync(new SubmissionDraftRequest(gameId, score, username, screenshot), user);
        var (created, xpGain) = await SaveAsync(draft);
        await CheckAchievementsAsync(created.UserId);
        return (ToCreatorDto(created), xpGain);
    }

    /// <summary>
    /// Saves the submission and awards its XP in one transaction, so both succeed or both roll back. A unique
    /// index violation means a concurrent submission for the same user/game/day won the race.
    /// </summary>
    private async Task<(Submission Created, int XpGain)> SaveAsync(Submission draft)
    {
        try
        {
            return await transactions.RunAsync(() => CreateWithXpAwardAsync(draft));
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException(SubmissionDraftBuilder.AlreadySubmittedMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating submission for game {GameId} by user {UserId}", draft.GameId, draft.UserId);
            throw;
        }
    }

    /// <summary>The user row is first loaded inside the transaction, which serializes the XP read-modify-write with other writers.</summary>
    private async Task<(Submission Created, int XpGain)> CreateWithXpAwardAsync(Submission draft)
    {
        var created = await submissions.CreateAsync(draft);
        var xpGain = created.UserId is int userId
            ? await xp.AwardForSubmissionAsync(userId, created.Id, created.ScoringDay)
            : 0;
        return (created, xpGain);
    }

    /// <summary>Runs after the commit: achievement failures are logged by the service and never undo the submission.</summary>
    private async Task CheckAchievementsAsync(int? userId)
    {
        if (userId is not int signedInUserId) return;

        await achievements.CheckAndAwardAsync(signedInUserId, AchievementTrigger.Submission);
        await achievements.CheckAndAwardAsync(signedInUserId, AchievementTrigger.LevelUp);
    }

    /// <summary>The creator may always see their own submission; it is on the current day, so its screenshot URL is signed.</summary>
    private SubmissionDto ToCreatorDto(Submission created)
    {
        var dto = SubmissionDtoHelper.ToDtoWithScoringDay(created, created.ScoringDay.Date);
        return SubmissionDtoHelper.SignScreenshot(dto, screenshotSigner, sign: true);
    }
}
