# PHASE 7 — Migration and compatibility (Deliverable)

## A) Root cause summary

- **Primary:** Legacy role name `Administrator` exists in DB, Identity (`AspNetRoles`/`AspNetUserRoles`), and tokens alongside the canonical name `Admin`. Policies and code must treat both as the same capability (e.g. UsersView/UsersManage) without breaking existing users or tokens.
- **Secondary:** No single place defined “canonical” vs “legacy” or how to migrate; removal of the alias after migration was undocumented.

---

## B) Exact changed files with rationale

| File | Rationale |
|------|------------|
| **backend/Auth/RoleCanonicalization.cs** (new) | Single place for canonical role constants and legacy→canonical map; used by auth and reset-password logic. |
| **backend/Controllers/AuthController.cs** | Emit canonical role in JWT via `RoleCanonicalization.GetCanonicalRole(primaryRole)` so new logins get `Admin` in token. |
| **backend/Controllers/UserManagementController.cs** | Use canonical role for “only SuperAdmin can reset SuperAdmin” so `Administrator` is never treated as SuperAdmin. |
| **backend/Migrations/20260308140000_CanonicalizeLegacyRoleNames.cs** (new) | One-time data migration: `AspNetUsers.role` and `AspNetUserRoles` from Administrator → Admin. |
| **backend/Scripts/CanonicalizeLegacyRoleNames.sql** (new) | Standalone SQL for environments that apply updates manually (no EF). |
| **ai/08_ROLE_MIGRATION.md** (new) | Documents alias handling, how to run migration, and **steps to remove alias after migration milestone**. |
| **backend/KasseAPI_Final.Tests/RoleCanonicalizationTests.cs** (new) | Unit tests for `GetCanonicalRole` and legacy alias list. |

**Unchanged by design:** `Program.cs` policies still list both `"Admin"` and `"Administrator"` so existing tokens remain valid until re-login or expiry. Alias removal is documented in `ai/08_ROLE_MIGRATION.md` for after the migration milestone.

---

## C) Before/after snippets for critical auth and password logic

### C1) JWT role claim (AuthController)

**Before:**

```csharp
var roleValue = roleForToken ?? user.Role ?? "User";
var token = new JwtSecurityToken(
    ...
    new Claim(ClaimTypes.Role, roleValue)
```

**After:**

```csharp
var roleValue = RoleCanonicalization.GetCanonicalRole(roleForToken ?? user.Role ?? "User");
var token = new JwtSecurityToken(
    ...
    new Claim(ClaimTypes.Role, roleValue)
```

### C2) Reset-password: only SuperAdmin can reset SuperAdmin (UserManagementController)

**Before:**

```csharp
var actorRole = GetCurrentUserRole();
if (string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(actorRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
```

**After:**

```csharp
var actorRole = GetCurrentUserRole();
var targetCanonicalRole = RoleCanonicalization.GetCanonicalRole(user.Role);
var actorCanonicalRole = RoleCanonicalization.GetCanonicalRole(actorRole);
if (string.Equals(targetCanonicalRole, RoleCanonicalization.Canonical.SuperAdmin, StringComparison.Ordinal) &&
    !string.Equals(actorCanonicalRole, RoleCanonicalization.Canonical.SuperAdmin, StringComparison.Ordinal))
```

### C3) New helper (Auth/RoleCanonicalization.cs)

```csharp
private static readonly IReadOnlyDictionary<string, string> LegacyToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "Administrator", Canonical.Admin },
};
public static string GetCanonicalRole(string? role)
{
    if (string.IsNullOrWhiteSpace(role)) return string.Empty;
    return LegacyToCanonical.TryGetValue(role.Trim(), out var canonical) ? canonical : role.Trim();
}
```

---

## D) Test commands and exact results

```powershell
cd c:\Users\Juke\local-projects\Regkasse\backend
dotnet build KasseAPI_Final.csproj
# Build succeeded. 0 Error(s)

dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~UserManagementControllerUserLifecycleTests|FullyQualifiedName~AuthControllerTests|FullyQualifiedName~RoleCanonicalizationTests" --no-build
# Passed!  - Failed: 0, Passed: 28, Skipped: 0, Total: 28
```

(RoleCanonicalizationTests add 5 tests; policy + lifecycle + AuthController total 23 → 28.)

**Apply migration (when DB is ready):**

```powershell
dotnet ef database update --project . --startup-project .
# Or run SQL manually: psql -d YourDatabase -f Scripts/CanonicalizeLegacyRoleNames.sql
```

---

## E) Remaining risks / follow-up items

1. **Migration Designer:** `20260308140000_CanonicalizeLegacyRoleNames` is data-only and has no Designer file. The next `dotnet ef migrations add` will base the new migration on the current snapshot; no schema change is introduced.
2. **Token lifetime:** Existing JWTs with role `Administrator` remain valid until expiry; policies still accept both. After alias removal (see `ai/08_ROLE_MIGRATION.md`), force re-login or wait for expiry so all tokens use `Admin`.
3. **Frontend:** `frontend-admin` still lists `'Administrator'` in role constants; remove it when performing the alias-removal milestone.
4. **RoleSeedData:** Still seeds both `Administrator` and `Admin`; stop seeding `Administrator` in the removal step.
5. **Rollback:** Data migration has no safe automatic Down; revert via backup or manual SQL if required.
