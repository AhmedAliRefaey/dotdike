using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Localization;

public sealed class LocalizedIdentityErrors(IStringLocalizer<UiText> text) : IdentityErrorDescriber
{
    private IdentityError Error(string code, string message, params object[] values) =>
        new() { Code = code, Description = text[message, values] };

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "The request could not be completed. Please try again.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "The account changed. Reload the page and try again.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "The current password is incorrect.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), "Passwords must be at least {0} characters.", length);
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Passwords must contain a symbol.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Passwords must contain a number.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Passwords must contain a lowercase letter.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Passwords must contain an uppercase letter.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), "Passwords must contain at least {0} different characters.", uniqueChars);
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "Use a valid work email address.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Enter a valid email address.");
}
