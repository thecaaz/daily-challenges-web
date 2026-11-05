using DailyChallenges.Exceptions;
using DailyChallenges.Services;
using Xunit;

namespace DailyChallenges.Tests;

public class CredentialRulesTests
{
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("  John.Doe_1-x  ", "John.Doe_1-x")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", "abcdefghijklmnopqrstuvwxyz012345")] // 32
    public void Valid_usernames_are_trimmed(string input, string expected)
    {
        Assert.Equal(expected, CredentialRules.ValidateUsername(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ab  ")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")] // 33
    [InlineData("has space")]
    [InlineData("tab\tname")]
    [InlineData("ümlaut")]
    [InlineData("na\nme")]
    [InlineData("semi;colon")]
    public void Invalid_usernames_throw_validation(string? input)
    {
        Assert.Throws<ValidationException>(() => CredentialRules.ValidateUsername(input));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    public void Password_length_is_8_to_128(string? password, bool valid)
    {
        if (valid) CredentialRules.ValidatePassword(password);
        else Assert.Throws<ValidationException>(() => CredentialRules.ValidatePassword(password));
    }

    [Fact]
    public void Password_over_128_is_rejected()
    {
        CredentialRules.ValidatePassword(new string('p', 128));
        Assert.Throws<ValidationException>(() => CredentialRules.ValidatePassword(new string('p', 129)));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=1+1", "\"'=1+1\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("-1", "\"'-1\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("\tx", "\"'\tx\"")]
    [InlineData("\rx", "\"'\rx\"")]
    [InlineData("a=b", "\"a=b\"")]
    public void Csv_text_cells_are_quoted_and_formula_prefixes_neutralized(string? input, string expected)
    {
        Assert.Equal(expected, AdminUserService.EscapeCsvText(input));
    }
}
