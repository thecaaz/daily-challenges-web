namespace DailyChallenges.Exceptions
{
    /// <summary>Base type for expected business-rule failures; the middleware maps each subtype to a status code.</summary>
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message) { }
    }

    /// <summary>404: the resource does not exist, or the caller may not know it exists.</summary>
    public sealed class NotFoundException : DomainException
    {
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>403: the caller is authenticated but not allowed to do this.</summary>
    public sealed class ForbiddenException : DomainException
    {
        public ForbiddenException(string message) : base(message) { }
    }

    /// <summary>409: the request conflicts with current state (duplicates, already done).</summary>
    public sealed class ConflictException : DomainException
    {
        public ConflictException(string message) : base(message) { }
    }

    /// <summary>400: the request is invalid.</summary>
    public sealed class ValidationException : DomainException
    {
        public ValidationException(string message) : base(message) { }
    }

    /// <summary>401: login / token checks failed.</summary>
    public sealed class AuthenticationFailedException : DomainException
    {
        public AuthenticationFailedException(string message) : base(message) { }
    }
}
