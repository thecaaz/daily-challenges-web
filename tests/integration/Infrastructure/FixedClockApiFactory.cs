using DailyChallenges.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>An ApiFactory whose clock is fixed, so "the current scoring day" is deterministic.</summary>
public sealed class FixedClockApiFactory : ApiFactory
{
    public static readonly DateTime Now = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    public FixedTimeProvider Clock { get; } = new(Now);

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }
}
