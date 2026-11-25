using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IGameCommandService
{
    /// <summary>Throws ValidationException for an invalid reset time, URL or ranking mode.</summary>
    Task<GameDto> CreateAsync(GameCreateRequest request);

    /// <summary>
    /// Updates a game. Omitted (null) fields stay unchanged; "" clears Url and Description.
    /// A ResetTime change re-derives the stored ScoringDay of the game's not-yet-finalized submissions
    /// (and of their XpEvents) in the same transaction, or fails with a ConflictException.
    /// Throws ValidationException for invalid input and KeyNotFoundException for a missing game.
    /// </summary>
    Task<GameDto> UpdateAsync(int id, GameUpdateRequest request);

    Task DeleteAsync(int id);
}
