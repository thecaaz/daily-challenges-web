namespace DailyChallenges.DTOs;

public sealed record RegisterDto(string Username, string Password);
public sealed record LoginDto(string Username, string Password);
public sealed record AuthResultDto(UserDto User, string AccessToken, int ExpiresInSeconds);
