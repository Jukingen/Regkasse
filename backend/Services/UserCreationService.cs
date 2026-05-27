using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Validators;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services;

public sealed class UserCreationService : IUserCreationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserUniquenessValidationService _uniquenessValidation;

    public UserCreationService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserUniquenessValidationService uniquenessValidation)
    {
        _db = db;
        _userManager = userManager;
        _uniquenessValidation = uniquenessValidation;
    }

    public async Task<(string UserName, string? Error)> ResolveUsernameAsync(
        string? requestedUserName,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requestedUserName))
        {
            var trimmed = requestedUserName.Trim();
            var reservedError = UsernameValidation.ValidateAssignableUsername(trimmed);
            if (reservedError != null)
                return (trimmed, reservedError);

            if (await _uniquenessValidation.IsUserNameTakenByOtherUserAsync(trimmed, excludeUserId: null)
                    .ConfigureAwait(false))
                return (trimmed, UsernameConflictMessages.Detail(trimmed));

            return (trimmed, null);
        }

        var generated = await UniqueUsernameGenerator
            .AllocateUniqueUsernameAsync(_db, _userManager, role, cancellationToken)
            .ConfigureAwait(false);

        return (generated, null);
    }
}
