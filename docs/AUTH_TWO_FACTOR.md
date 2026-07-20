# SuperAdmin two-factor authentication (2FA)

Technical documentation (English). Operator-facing FA copy is i18n (`common.auth.twoFactor.*`).

## Summary

| Environment | SuperAdmin | Other roles |
|-------------|------------|-------------|
| **Development** (`TwoFactorAuth:BypassInDevelopment=true`, default) | No 2FA challenge — tokens issued after password | Unchanged |
| **Production / Staging** (`TwoFactorAuth:Enabled=true`) | TOTP required after password | Unchanged |

- **Method:** ASP.NET Identity authenticator TOTP (Authenticator app). Not SMS/phone.
- **Scope:** SuperAdmin only (FA `clientApp: "admin"` and any SuperAdmin login that hits the gate).
- **Hub config:** `TwoFactorAuth` section (`TwoFactorAuthOptions`).

## Configuration

### `TwoFactorAuth` (preferred)

| Key | Dev default | Prod default | Meaning |
|-----|-------------|--------------|---------|
| `Enabled` | `false` in local Dev template / `true` in example | `true` | Master switch. `false` → never challenge SuperAdmin. |
| `BypassInDevelopment` | `true` | `false` | When `true` **and** `IHostEnvironment.IsDevelopment()`, skip challenge at login. **Ignored outside Development** (fail-closed). |
| `TestToken` | `"123456"` | `""` | Accepted as code substitute **only in Development** (with `DEV-2FA-BYPASS`). Never honored in Production. |

Example — `appsettings.Development.json`:

```json
{
  "TwoFactorAuth": {
    "Enabled": false,
    "BypassInDevelopment": true,
    "TestToken": "123456"
  }
}
```

Example — Production:

```json
{
  "TwoFactorAuth": {
    "Enabled": true,
    "BypassInDevelopment": false,
    "TestToken": ""
  }
}
```

Env overrides: `TwoFactorAuth__Enabled`, `TwoFactorAuth__BypassInDevelopment`, `TwoFactorAuth__TestToken`.

### Legacy `Auth:RequireSuperAdminTwoFactor`

Nullable override used by unit tests / forced staging:

- `null` (default) → follow `TwoFactorAuth` + environment rules above.
- `true` / `false` → force challenge on/off **only if** `TwoFactorAuth:Enabled` is true and Development bypass does not apply.

Prefer `TwoFactorAuth` for new configuration.

## Login flow

1. `POST /api/Auth/login` — password + role/tenant checks as usual.
2. If SuperAdmin 2FA is **not** required → issue access/refresh tokens (`requires2FA: false`, `isDevelopment` when applicable).
3. If required → **no tokens**; response:

```json
{
  "requires2FA": true,
  "requires2FASetup": true,
  "twoFactorToken": "<opaque pending token>",
  "isDevelopment": false,
  "authenticatorKey": "...",
  "authenticatorUri": "otpauth://totp/...",
  "developmentBypassCode": null
}
```

- `requires2FASetup: true` when Identity `TwoFactorEnabled` is false — client shows shared key for authenticator enrollment.
- Pending token TTL: ~5 minutes, single-use (`ITwoFactorChallengeService`).

4. `POST /api/Auth/verify-2fa`:

```json
{ "twoFactorToken": "...", "code": "123456" }
```

Success → same token payload as a normal login. Failed TOTP → `401` `TWO_FACTOR_INVALID`. Expired pending → `401` `TWO_FACTOR_CHALLENGE_EXPIRED`.

### Development verify codes

Only when API environment is Development:

| Code | Source |
|------|--------|
| `DEV-2FA-BYPASS` | Fixed (`ITwoFactorService.DevelopmentBypassToken`) |
| `TwoFactorAuth:TestToken` (default `123456`) | Config |

## Frontend Admin

- `LoginForm` — if login returns `requires2FA`, shows `TwoFactorAuth`.
- **Development UI:** info `Alert` + Continue → calls `verify-2fa` with bypass code (`useEnvironment` / `challenge.isDevelopment`).
- **Production UI:** TOTP input (`RealTwoFactorAuth`).
- Files: `frontend-admin/src/features/auth/components/TwoFactorAuth.tsx`, `LoginForm.tsx`.

## Backend files

| Piece | Path |
|-------|------|
| Options | `backend/Configuration/TwoFactorAuthOptions.cs` |
| Verify / Dev codes | `backend/Services/TwoFactor/TwoFactorService.cs` |
| Pending challenge | `backend/Services/Auth/TwoFactorChallengeService.cs` |
| Gate + endpoints | `backend/Controllers/AuthController.cs` (`login`, `verify-2fa`) |
| DTOs | `backend/Models/DTOs/TwoFactorDtos.cs` |

## Testing

```bash
cd backend
dotnet test --filter "FullyQualifiedName~TwoFactor|FullyQualifiedName~AuthControllerTests"
```

**Force 2FA UI in Development:** set `TwoFactorAuth:Enabled=true`, `BypassInDevelopment=false`, restart API; SuperAdmin login should return a challenge; Continue / `DEV-2FA-BYPASS` / `TestToken` completes login.

## Security notes

- Do not set a Production `TestToken` that is accepted outside Development — verify path ignores bypass when `!IsDevelopment()`.
- Do not enable `BypassInDevelopment` expecting it to work in Staging/Production hosts — host environment must be Development.
- Authenticator secrets are Identity-managed; the API never returns a live Production TOTP code.

## Related

- [`AGENTS.md`](../AGENTS.md) § Authentication — SuperAdmin 2FA
- [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) § Authentication
- [`API_CONTRACTS.md`](API_CONTRACTS.md) § Authentication
- [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md)
