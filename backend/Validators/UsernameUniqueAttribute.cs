using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KasseAPI_Final.Validators;

/// <summary>
/// Ensures <see cref="ApplicationUser.NormalizedUserName"/> is not already used (case-insensitive).
/// For PATCH username, exclude the route <c>id</c> user via <see cref="IHttpContextAccessor"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class UsernameUniqueAttribute : ValidationAttribute
{
    public const string DefaultError = "Username is already taken (case-insensitive). Please choose another.";

    public UsernameUniqueAttribute()
        : base(DefaultError)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string username || string.IsNullOrWhiteSpace(username))
            return ValidationResult.Success;

        var userManager = validationContext.GetService(typeof(UserManager<ApplicationUser>))
            as UserManager<ApplicationUser>;
        if (userManager == null)
            return ValidationResult.Success;

        var excludeUserId = ResolveExcludeUserId(validationContext);
        var taken = IdentityLoginLookup
            .IsUserNameTakenByOtherUserAsync(userManager, username.Trim(), excludeUserId)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        if (!taken)
            return ValidationResult.Success;

        return new ValidationResult(
            UsernameConflictMessages.Detail(username.Trim()),
            new[] { validationContext.MemberName ?? nameof(username) });
    }

    private static string? ResolveExcludeUserId(ValidationContext validationContext)
    {
        var httpAccessor = validationContext.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
        var routeId = httpAccessor?.HttpContext?.GetRouteData()?.Values.TryGetValue("id", out var raw) == true
            ? raw?.ToString()
            : null;
        if (!string.IsNullOrWhiteSpace(routeId))
            return routeId.Trim();

        if (validationContext.Items.TryGetValue(UsernameUniqueValidationKeys.ExcludeUserId, out var item)
            && item is string fromItems
            && !string.IsNullOrWhiteSpace(fromItems))
        {
            return fromItems.Trim();
        }

        return null;
    }
}

/// <summary>Optional <see cref="ValidationContext.Items"/> key when route id is unavailable.</summary>
public static class UsernameUniqueValidationKeys
{
    public const string ExcludeUserId = "UsernameUnique.ExcludeUserId";
}
