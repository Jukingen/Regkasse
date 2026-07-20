using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Validators;

/// <summary>
/// Explicit password complexity checks aligned with <see cref="IdentityOptions.Password"/>
/// and <see cref="Services.PasswordErrorTranslator"/> Identity error codes.
/// Replaces the default <see cref="PasswordValidator{TUser}"/> (same rules, single validator).
/// </summary>
public sealed class RegkassePasswordValidator<TUser> : IPasswordValidator<TUser>
    where TUser : class
{
    private readonly IdentityErrorDescriber _describer;
    private readonly IOptions<IdentityOptions> _options;

    public RegkassePasswordValidator(
        IdentityErrorDescriber describer,
        IOptions<IdentityOptions> options)
    {
        _describer = describer;
        _options = options;
    }

    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
    {
        var opts = _options.Value.Password;
        var errors = new List<IdentityError>();

        if (string.IsNullOrWhiteSpace(password) || password.Length < opts.RequiredLength)
        {
            errors.Add(_describer.PasswordTooShort(opts.RequiredLength));
        }

        if (password is null)
        {
            return Task.FromResult(IdentityResult.Failed(errors.ToArray()));
        }

        if (opts.RequireUppercase && !password.Any(char.IsUpper))
            errors.Add(_describer.PasswordRequiresUpper());

        if (opts.RequireLowercase && !password.Any(char.IsLower))
            errors.Add(_describer.PasswordRequiresLower());

        if (opts.RequireDigit && !password.Any(char.IsDigit))
            errors.Add(_describer.PasswordRequiresDigit());

        if (opts.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            errors.Add(_describer.PasswordRequiresNonAlphanumeric());

        if (opts.RequiredUniqueChars > 0
            && password.Distinct().Count() < opts.RequiredUniqueChars)
        {
            errors.Add(_describer.PasswordRequiresUniqueChars(opts.RequiredUniqueChars));
        }

        return Task.FromResult(
            errors.Count > 0
                ? IdentityResult.Failed(errors.ToArray())
                : IdentityResult.Success);
    }
}
