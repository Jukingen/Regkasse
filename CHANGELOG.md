# Changelog

All notable changes to this project are documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Older waves: [`docs/CHANGELOG_RECENT.md`](docs/CHANGELOG_RECENT.md), [`docs/CHANGELOG_TENANT_MANAGEMENT.md`](docs/CHANGELOG_TENANT_MANAGEMENT.md).

---

## [Unreleased]

### Added

- **Username-based login:** Users can sign in with either **email** or **username** (`loginIdentifier` on `POST /api/Auth/login`).
- **Auto-generated usernames:** **Schnell anlegen** (Quick Create) and manual admin user create generate unique login names when `userName` is omitted (`{rolePrefix}{n}`, e.g. `manager1`, `cashier2`).
- **Username display:** FA success modals show **username**, **email**, and one-time password with per-field copy and **copy all** (`QuickUserSuccessModal`, `UserCreatedSuccessModal`).
- **API contract supplement:** [`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) — login and user-create request/response deltas.

### Changed

- `POST /api/Auth/login` accepts **`loginIdentifier`** (email or username); resolves user via `FindByEmailAsync` then `FindByNameAsync`.
- **Frontend Admin** login form accepts email or username (`LoginForm` → `loginIdentifier`, legacy `email` mirrored).
- **Frontend POS** login screen accepts email or username (`login.tsx`, `authService.buildLoginPayload`).
- Tenant user create: `UserName` is no longer forced equal to email; optional `userName` on `CreateTenantUserRequest`, `CreateQuickTenantUserRequest`, `AdminCreateUserRequest`.
- Quick Create response and manual create responses include **`userName`** (`CreateTenantUserResultDto`, `AdminCreateUserResponseDto`).

### Deprecated

- **`email`** field on login requests — still supported when `loginIdentifier` is empty; new clients should use **`loginIdentifier`**.

### Backend

- Added **`UniqueUsernameGenerator`** and **`IUserCreationService`** / **`UserCreationService`** for username allocation and uniqueness checks (`IUserUniquenessValidationService.IsUserNameTakenByOtherUserAsync`).
- Updated **`TenantUserService`** (`CreateAsync`, `CreateQuickAsync`), **`AdminUsersController.Create`**, and **`QuickUserGeneratorService`** for optional/auto `userName`.
- **`LoginModel`** with `loginIdentifier` + `ResolveLoginIdentifier()`; **`AuthController`** username/email lookup.
- Quick Create email pattern: `{role}_{random6}@{tenantSlug}.regkasse.at` (`QuickUserEmailGenerator`).

### Frontend-Admin

- **Schnell anlegen** tab: username pattern preview (`quickUserPreview.ts`, `CreateUserModal`).
- **`QuickUserSuccessModal`**: username, email, password rows + clipboard helpers (`clipboard.ts`).
- i18n: login identifier label/validation; quick-create result labels (de / en / tr).
- Documentation: [`frontend-admin/README.md`](frontend-admin/README.md), [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md) (Authentication).

### Frontend-POS

- Login field and persistence: **`loginIdentifier`** (`savedLoginIdentifier`, legacy `savedUsername` fallback).
- **`authService`**: `buildLoginPayload` sends `loginIdentifier` + legacy `email`.
- i18n: `auth.loginIdentifierPlaceholder`, `validation.loginIdentifierRequired` (de / en / tr).
- Documentation: [`frontend/README.md`](frontend/README.md) (Authentication).
