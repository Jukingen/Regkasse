using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services
{
    /// <summary>Resolves actor display names via UserManager (FirstName LastName or UserName).</summary>
    public class ActorDisplayNameResolver : IActorDisplayNameResolver
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ActorDisplayNameResolver(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(IList<string> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                return new Dictionary<string, string>();

            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
                .ToListAsync();

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var u in users)
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                dict[u.Id] = string.IsNullOrEmpty(name) ? u.UserName ?? u.Id : name;
            }
            return dict;
        }

        public async Task<IReadOnlyDictionary<string, string>> ResolveLoginLabelsAsync(IList<string> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                return new Dictionary<string, string>();

            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.UserName })
                .ToListAsync();

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var u in users)
            {
                var label = !string.IsNullOrWhiteSpace(u.Email)
                    ? u.Email.Trim()
                    : (!string.IsNullOrWhiteSpace(u.UserName) ? u.UserName.Trim() : null);
                if (!string.IsNullOrEmpty(label))
                    dict[u.Id] = label;
            }
            return dict;
        }
    }
}
