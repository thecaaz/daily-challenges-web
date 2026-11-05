namespace DailyChallenges.TestUtilities;

/// <summary>A TimeProvider whose "now" is set by the test. Shared by the unit and integration test projects.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTime utcNow) => SetUtcNow(utcNow);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTime utcNow) =>
        _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
}
