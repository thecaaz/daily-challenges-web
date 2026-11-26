namespace DailyChallenges.Exceptions;

/// <summary>Base type for expected business-rule failures; the middleware maps each subtype to a status code.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>404: the resource does not exist, or the caller may not know it exists.</summary>
public sealed class NotFoundException(string message) : DomainException(message);

/// <summary>403: the caller is authenticated but not allowed to do this.</summary>
public sealed class ForbiddenException(string message) : DomainException(message);

/// <summary>409: the request conflicts with current state (duplicates, already done).</summary>
public sealed class ConflictException(string message) : DomainException(message);

/// <summary>400: the request is invalid.</summary>
public sealed class ValidationException(string message) : DomainException(message);

/// <summary>401: login / token checks failed.</summary>
public sealed class AuthenticationFailedException(string message) : DomainException(message);
