namespace DailyChallenges.Repositories;

/// <summary>
/// Runs work inside one database transaction (BEGIN IMMEDIATE on SQLite, which serializes writers).
/// The transaction commits when the work completes; on any exception it rolls back, clears the
/// change tracker so no half-applied entities leak into later saves, and rethrows.
/// Cancelling the token is such an exception: the work is rolled back, never half-committed.
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct);
}
