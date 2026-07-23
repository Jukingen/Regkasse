# Tools (`tools/`)

Auxiliary utilities that are **not** the main API or UI packages.  
**Last reviewed:** 2026-07-21

| Path | Purpose | Status |
|------|---------|--------|
| [`LicenseGenerator.Core/`](LicenseGenerator.Core/) | Shared `LicenseIssuer` library (referenced by **backend**) | **Required** |
| [`LicenseGenerator/`](LicenseGenerator/) | CLI license key issuer (`net10.0`) | Active |
| [`LicenseGenerator.Web/`](LicenseGenerator.Web/) | Localhost web UI for issuance (`http://127.0.0.1:5055`) | Active |
| [`LicenseTools.sln`](LicenseTools.sln) | Solution for the three license projects | Active |
| [`i18n/`](i18n/) | Thin wrappers → [`localization/`](../localization/) CSV/validate | Convenience aliases |
| [`redis/`](redis/) | Download target for portable Windows Redis | Local only (gitignored binaries) |

> Path note: the license CLI lives at **`tools/LicenseGenerator/`** (PascalCase), not `license-generator/`. Backend: `ProjectReference` → `tools/LicenseGenerator.Core`.

---

## License generator

Issues offline/dev **REGK-…** keys + RS256 JWT via `LicenseIssuer` (same Core API used by `backend/Services/LicenseIssuanceService.cs` for admin flows).

**Docs:** [`docs/BILLING_TENANT_LICENSE.md`](../docs/BILLING_TENANT_LICENSE.md), [`docs/LICENSE_SYSTEM.md`](../docs/LICENSE_SYSTEM.md).

### Build

```bash
dotnet build tools/LicenseTools.sln -c Release
```

### CLI (`LicenseGenerator`)

```bash
# Generate RSA key pair (keep private key offline)
dotnet run --project tools/LicenseGenerator -- init-keys --output-dir ./secrets-license

# Issue floating license (omit --machine-hash)
dotnet run --project tools/LicenseGenerator -- \
  --customer "Firma GmbH" \
  --expiry 2027-12-31 \
  --private-key ./secrets-license/license_private.pem \
  --output ./license.txt

dotnet run --project tools/LicenseGenerator -- --help
```

Embed **public** PEM only in backend config:

```json
"License": {
  "OfflineVerificationPublicKeyPem": "-----BEGIN RSA PUBLIC KEY-----..."
}
```

Private keys and `tools/**/appsettings.json` are gitignored (`license_private.pem`, see root `.gitignore`).

### Web UI (`LicenseGenerator.Web`)

```bash
# Set private key path (env or appsettings — use appsettings.example.json as template)
set LICENSE_GENERATOR_PRIVATE_KEY_PATH=C:\path\to\license_private.pem
dotnet run --project tools/LicenseGenerator.Web
# Open http://127.0.0.1:5055
```

Internal tool — binds to localhost by default. Do not expose publicly.

### Smoke check (verified 2026-07-21)

`dotnet build tools/LicenseTools.sln` succeeded; CLI `init-keys` + `--customer` issue produced a `REGK-…` key and JWT.

---

## i18n helpers (`tools/i18n/`)

**Canonical tooling:** [`localization/`](../localization/README.md) (CI gates, budgets, namespace manifest).

These scripts are **thin wrappers** that spawn localization scripts:

| Wrapper | Forwards to |
|---------|-------------|
| `export-csv.mjs` | `localization/scripts/export-translations.mjs` |
| `import-csv.mjs` | `localization/scripts/import-translations.mjs` |
| `validate.mjs` | `localization/scripts/validate-translations.mjs` |

```bash
# Prefer root / localization package:
npm run i18n:validate   # if defined at root
cd localization && npm run validate

# Or wrappers (repo root cwd):
node tools/i18n/validate.mjs --project frontend-admin
node tools/i18n/export-csv.mjs --project frontend-admin --dry-run
```

Do not reintroduce a parallel key-validation implementation under `tools/i18n/`.

---

## Redis (`tools/redis/`)

Portable Windows Redis download directory. Binaries are **not** in git.

```powershell
.\scripts\start-redis-dev.ps1
```

Details: [`redis/README.md`](redis/README.md), `backend/CONFIGURATION.md`.

---

## Removed / do not revive

| Item | Why |
|------|-----|
| `LicenseTools.slnx` | Empty stub; use `LicenseTools.sln` |
| `tools/i18n/projects.mjs` | Duplicate manifest logic; wrappers use `localization/` |
| Legacy standalone `tools/i18n/validate` key-scan | Broken vs manifest; replaced by localization wrapper |

---

## Conventions

- Do not commit production private keys or filled `appsettings.json` under these projects.
- Prefer English log messages.
- License **sales** (mandant billing) go through Super Admin API/FA — this tool is for offline/dev key material and Core library reuse.
- Docker Redis is fine instead of `tools/redis` if preferred.

## Related

- Backend license config: `backend/CONFIGURATION.md`
- Billing: `docs/BILLING_TENANT_LICENSE.md`
- Localization CI: `localization/README.md`, `.github/workflows/localization-validation.yml`
- Scripts index: [`../scripts/README.md`](../scripts/README.md)

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
