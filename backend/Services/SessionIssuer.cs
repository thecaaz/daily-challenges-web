using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;

namespace DailyChallenges.Services;

/// <summary>Starts a session: an access token in the response body and a fresh refresh cookie.</summary>
public sealed class SessionIssuer(JwtTokenFactory tokens, RefreshTokenCookie cookie, LevelCalculator levels, TimeProvider time)
{
    public AuthResultDto Issue(User user, HttpResponse response)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken(user);
        cookie.Write(response, refreshToken);

        var userDto = DtoMapper.ToDto(user, levels, time.GetUtcNow().UtcDateTime);
        return new AuthResultDto(userDto, accessToken.Value, (int)tokens.AccessTokenLifetime.TotalSeconds);
    }
}
