# Helpers & extensions guardrails

## Layout

| Location | Role |
|----------|------|
| `backend/Helpers/` | Username / password / login lookup utilities |
| `backend/Utils/` | **Removed** — dead `MachineFingerprint` (license binding lives in `LicenseStorageService`) |
| `backend/Extensions/` | **Does not exist** — keep domain `*Extensions` next to their owners |

## Canonical password API

Use **`PasswordGenerator.GenerateSecurePassword(length)`** only (default 12; provisioning uses 16).

Do not reintroduce local RNG password builders in services.

## Canonical username APIs

| Concern | API |
|---------|-----|
| Format + reserved | `UsernameValidation` / `ReservedUsernames` |
| Case-insensitive lookup / uniqueness | `IdentityLoginLookup` / `IUserUniquenessValidationService` |
| Quick-create names | `UniqueUsernameGenerator` + `QuickUserEmailGenerator` |
| Change cooldown / age / SuperAdmin bypass | `UsernameChangeRateLimit` / `UsernameChangePolicy` / `UsernameChangeRestrictions` |

## Query extensions

Filter counting for payments/products goes through `BuildFilterSummary` on `PaymentQueryExtensions` / `ProductQueryExtensions`. Do not add parallel private counters on controllers.

## Tests

```bash
dotnet test --filter "FullyQualifiedName~PasswordGenerator|FullyQualifiedName~TenantProvisioningServiceTests|FullyQualifiedName~TenantUserServiceTests|FullyQualifiedName~IdentityLoginLookupTests|FullyQualifiedName~UniqueUsernameGeneratorTests|FullyQualifiedName~UsernameChange|FullyQualifiedName~ReservedUsernamesTests|FullyQualifiedName~AdminPaymentListFilterTests|FullyQualifiedName~QuickUserGeneratorServiceTests"
```
