using System.Text.RegularExpressions;
using DailyChallenges.Exceptions;

namespace DailyChallenges.Services
{
    /// <summary>Username and password rules shared by registration and the admin password reset.</summary>
    public static class CredentialRules
    {
        public const int UsernameMinLength = 3;
        public const int UsernameMaxLength = 32;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 128;

        private static readonly Regex UsernamePattern = new("^[A-Za-z0-9_.-]+\\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Trims the username (null becomes empty). Used for register and login lookups.</summary>
        public static string NormalizeUsername(string? username) => (username ?? string.Empty).Trim();

        /// <summary>Returns the trimmed username, or throws <see cref="ValidationException"/> when it breaks the rules.</summary>
        public static string ValidateUsername(string? username)
        {
            var name = NormalizeUsername(username);
            if (name.Length < UsernameMinLength || name.Length > UsernameMaxLength)
                throw new ValidationException($"Username must be {UsernameMinLength}-{UsernameMaxLength} characters long");
            if (!UsernamePattern.IsMatch(name))
                throw new ValidationException("Username may only contain letters, digits, '_', '.' and '-'");
            return name;
        }

        /// <summary>Throws <see cref="ValidationException"/> unless the password is 8–128 characters long.</summary>
        public static void ValidatePassword(string? password)
        {
            var length = password?.Length ?? 0;
            if (length < PasswordMinLength || length > PasswordMaxLength)
                throw new ValidationException($"Password must be {PasswordMinLength}-{PasswordMaxLength} characters long");
        }
    }
}
