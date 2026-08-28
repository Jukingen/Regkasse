# Security Policy

Regkasse is a **proprietary** multi-tenant POS platform (RKSV/TSE). We take reports of security issues seriously and ask that you disclose them responsibly so we can protect customers and fiscal integrity.

For deeper engineering guardrails see [`AGENTS.md`](AGENTS.md), [`ai/05_SECURITY_COMPLIANCE.md`](ai/05_SECURITY_COMPLIANCE.md), and [`docs/`](docs/) (multi-tenant, CSRF, backup, auth).

---

## Reporting a Vulnerability

**Do not** open a public GitHub issue, discussion, or pull request for security vulnerabilities (credential leaks, auth bypass, tenant isolation flaws, RCE, XSS with session impact, fiscal/audit tampering, etc.).

### How to report

1. Email **[security@regkasse.at](mailto:security@regkasse.at)** with a clear subject, e.g. `Security report: <short summary>`.
2. If you use GitHub and private vulnerability reporting is enabled for this repository, you may also use **Security → Advisories → Report a vulnerability**.

### Please include

- Affected component (API, Admin, POS, Sites, Docker, scripts) and approximate version / commit / deploy date
- Description of the issue and potential impact (especially **cross-tenant** or **fiscal/audit** impact)
- Step-by-step reproduction or a minimal proof of concept (no mass exploitation)
- Any logs, screenshots, or request/response samples with **secrets redacted** (passwords, JWT, voucher codes, PEMs, connection strings)

### What to expect

| Step | Typical timing |
|------|----------------|
| Acknowledgement | Within **3 business days** |
| Initial severity assessment | Within **10 business days** |
| Fix / mitigation plan | Depends on severity; critical issues are prioritized |

We may ask follow-up questions. Please keep the report confidential until we confirm a fix or coordinated disclosure date.

**Out of scope for this mailbox (use normal support / product channels):** general feature requests, non-security bugs already tracked publicly, license or billing questions unrelated to security.

---

## Security Policy (supported versions)

Only actively maintained lines receive security fixes.

| Version / channel | Supported |
|-------------------|-----------|
| Current production deploy (`main` / tagged release in use at `*.regkasse.at`) | **Yes** |
| Latest development tip on `main` (pre-release) | Best-effort for critical issues |
| Older forks, abandoned branches, or unofficial builds | **No** |
| Dependencies of third-party packages alone (without Regkasse-specific impact) | Tracked via Dependabot / package audits; upgrade when we cut a supported release |

We do not publish a long SemVer support matrix in this proprietary monorepo. If you run a self-hosted or older snapshot, upgrade to the current supported revision before expecting a patch.

Security-related changes are documented in [`CHANGELOG.md`](CHANGELOG.md) when released.

---

## Secrets that must never be committed

Regkasse handles fiscal/RKSV material, payment credentials, and tenant isolation. **Real secrets belong in user secrets, environment variables, or a vault — never in git.**

### Files that must stay out of the repository

| Pattern / file | Why |
|----------------|-----|
| `backend/appsettings.json`, `appsettings.Development.json`, `appsettings.Staging.json`, `appsettings.Production.json` | May contain DB passwords, JWT keys, Fiskaly/Stripe/SMTP secrets |
| `.env`, `.env.local`, `.env.production`, `.env.*` (except `*.example`) | Compose and frontend runtime secrets |
| `**/user-secrets.json`, `**/secrets.json`, `credentials.json` | Copied .NET user-secrets or local login dumps |
| `*.pem`, `*.key`, `*.pfx`, `*.p12`, `*.crt`, `*.cer` | TLS and license signing private keys |
| `backend/App_Data/**` (except already-tracked demo product images) | License PEMs, dev-mail capture, local fiscal material |
| `id_rsa`, `id_ed25519`, `.netrc`, `auth.json` | SSH and registry credentials |

Tracked **templates only**:

- `backend/appsettings.example.json`, `appsettings.*.example.json`
- `.env.example`, `.env.*.example`, `frontend/.env.example`, `frontend-admin/.env.example`, `frontend-sites/.env.example`

Example files MUST use placeholders such as `YOUR_API_KEY_HERE`, `CHANGE_ME_…`, or empty strings — never live Fiskaly, Stripe, JWT, or database values.

### How to store secrets locally

**Backend (Development / local Staging):** [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (loaded by `ApplicationHost` in Development and Staging):

```bash
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
dotnet user-secrets set "Fiskaly:ApiKey" "YOUR_FISKALY_API_KEY_HERE"
dotnet user-secrets set "Fiskaly:ApiSecret" "YOUR_FISKALY_API_SECRET_HERE"
dotnet user-secrets set "Tse:Providers:fiskaly:ApiKey" "YOUR_FISKALY_API_KEY_HERE"
dotnet user-secrets set "Tse:Providers:fiskaly:ApiSecret" "YOUR_FISKALY_API_SECRET_HERE"
dotnet user-secrets set "PaymentGateway:Stripe:ApiKey" "YOUR_STRIPE_API_KEY_HERE"
dotnet user-secrets set "PaymentGateway:Stripe:WebhookSecret" "YOUR_STRIPE_WEBHOOK_SECRET_HERE"
```

License PEMs: put files under gitignored `backend/App_Data/` (or set `License:PrivateKeyPath`) — do not paste PEM bodies into JSON.

**Environment variables** (Docker / CI / Production): double-underscore keys, e.g. `ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, `Fiskaly__ApiKey`. Copy `.env.example` → `.env` (gitignored). See [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) and [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md).

**Frontends:** `frontend-admin/.env.local`, `frontend/.env`, `frontend-sites/.env.local` from each package’s `.env.example`. Do not put API keys that must stay server-side into `NEXT_PUBLIC_*` / `EXPO_PUBLIC_*`.

### Developer checklist

1. Copy `*.example` templates once; fill secrets via user-secrets or env — not by editing tracked examples.
2. Run `npm run verify:secrets` before a large commit; Husky runs the same scanner on every commit.
3. If a secret is committed or pasted into a ticket: **rotate it** (JWT, DB password, Fiskaly, Stripe, SMTP, license keys), invalidate sessions if JWT leaked, and treat git history as untrusted until rotated.
4. Never log passwords, voucher codes, raw PEMs, or unmasked JWTs.
5. Do not use `git commit --no-verify` / `SKIP_SECRET_SCAN=1` except a documented emergency; fix the files instead.

Documented **Development seed** passwords (`Admin123!` SuperAdmin, `DemoTenant1!` demo tenant admins) are local-only and must never be used in Production. Local Manager/Cashier passwords belong in `SMOKE_MANAGER_PASSWORD` / `SMOKE_CASHIER_PASSWORD`, not in scripts.

---

## Security Best Practices

### For reporters and operators

- Prefer least-privilege accounts; never share Super Admin credentials.
- Rotate JWT signing keys, DB passwords, and license PEMs if exposure is suspected.
- Restrict production admin/API hosts; do not enable Development-only tenant headers (`X-Tenant-Id` / `?tenant=`) outside Development.
- Keep backups and restore drills on isolated validation databases — never automatic production restore.

### For developers (this repository)

1. **Secrets** — Never commit real `appsettings.*.json` secrets, `.env`, PEMs, or API keys. Use user secrets / env vars ([`backend/CONFIGURATION.md`](backend/CONFIGURATION.md)). Templates stay as `*.example.json`.
2. **Tenant isolation** — Cross-tenant access must return **HTTP 404** (not 403). Do not use `IgnoreQueryFilters()` except approved Super Admin paths. Singletons that touch EF must use `IServiceScopeFactory`.
3. **API boundaries** — POS → `/api/pos/*`; Admin → `/api/admin/*`. Do not cross boundaries or extend legacy `/api/Payment`, `/api/Cart`, `/api/Product` for new features.
4. **Auth & sessions** — Prefer `loginIdentifier`; invalidate sessions on username/password changes (`SecurityStamp`). SuperAdmin 2FA: [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md). Logout (cookie split, stamp, CSRF): [`docs/AUTH_LOGOUT.md`](docs/AUTH_LOGOUT.md).
5. **CSRF** — Mutating Admin/browser flows must respect double-submit CSRF when enabled (`Security:Csrf`). Exempt only documented auth/health paths.
6. **Logging** — Never log passwords, voucher codes, raw card data, or unmasked PEMs/JWTs. Mask payment identifiers.
7. **Input validation** — Validate on the server; client checks are UX only. Follow username/email/tax-id rules in `AGENTS.md`.
8. **Dependencies** — Run package audits (`npm audit`, NuGet audit via `Directory.Build.props`); prefer Dependabot PRs; do not silence high/critical without a documented exception.
9. **Fiscal / RKSV** — Treat TSE signing, receipt chains, FinanzOnline outbox, and voucher ledgers as high-risk; read `ai/07_DO_NOT_TOUCH.md` before changes.
10. **Frontend Admin** — No static Ant Design `message` / `notification` / `Modal.confirm` for security-sensitive flows without app context; never put tokens in Zustand. See FA conventions in `AGENTS.md`.

### Useful internal references

| Doc | Topic |
|-----|--------|
| [`AGENTS.md`](AGENTS.md) | Always-on engineering and security rules |
| [`ai/05_SECURITY_COMPLIANCE.md`](ai/05_SECURITY_COMPLIANCE.md) | Tenancy, compliance posture |
| [`ai/07_DO_NOT_TOUCH.md`](ai/07_DO_NOT_TOUCH.md) | High-risk surfaces |
| [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md) | SuperAdmin 2FA |
| [`docs/AUTH_LOGOUT.md`](docs/AUTH_LOGOUT.md) | Logout security (cookies, stamp, CSRF) |
| [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md) | Tenant model |
| [`frontend-admin/SECURITY_AUDIT.md`](frontend-admin/SECURITY_AUDIT.md) | FA audit notes / cadence |
| [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) | User secrets / env keys |
| [`scripts/git-hooks/scan-secrets.mjs`](scripts/git-hooks/scan-secrets.mjs) | Pre-commit + CI secret scanner |
| [`LICENSE`](LICENSE) | Proprietary license |

---

## Preferred languages

Reports and coordination may be in **English** or **German**. Development explanations in this repo often use Turkish for IDE notes; security reports themselves should stay English or German for operational clarity.
