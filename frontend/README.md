# Regkasse POS (Mobile)

Expo Router + React Native + TypeScript cashier client for Austrian RKSV-compliant POS operations.

Historical dev notes: `DEVELOPMENT.md` (pointer) and `archive/DEVELOPMENT.md`.  
Repo onboarding: [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) · agents: [`AGENTS.md`](../AGENTS.md).

## Tech Stack

Versions from `package.json` (Expo SDK **56**). Prefer `npx expo install <pkg>` when upgrading Expo-locked packages so peer ranges stay aligned.

| Layer | Package / tool | Version (approx.) |
| ----- | -------------- | ----------------- |
| Runtime | Expo | `~56.0.16` |
| UI | React / React Native | `19.2.3` / `0.85.3` |
| Navigation | `expo-router` | `~56.2.15` |
| Language | TypeScript | `^7.0.2` (`strict`, `jsx: react-jsx`) |
| Bundler | Metro (`expo/metro-config`) | via Expo 56 |
| Transform | `babel-preset-expo` + `module-resolver` | via Expo / Babel 7 |
| HTTP | `axios` | `^1.18.1` |
| i18n | `i18next` / `react-i18next` | `^26` / `^17` |
| UI state | `zustand` | `^5` (ephemeral checkout UI only) |
| Animations | `react-native-reanimated` + worklets | `4.3.1` / `0.8.3` |
| Camera / QR | `expo-camera` | `~56.0.8` |
| Print / share | `expo-print` / `expo-sharing` | `~56.0.4` / `~56.0.22` |
| Lint / format | ESLint 9 flat + `eslint-config-universe` / Prettier | `^9.39` / `^16` / `^3.9` |
| Tests | Jest + `jest-expo` + RNTL | `^29` / `~56.0.5` / `^14` |

**Tooling notes**

- TypeScript **7**: no `baseUrl` in `tsconfig.json` (paths are relative to the config file). Run `npm run typecheck`.
- Babel: do **not** duplicate `transform-runtime` / Reanimated plugins — `babel-preset-expo` already provides them (`babel.config.js`).
- Metro: `maxWorkers: 2`, project-local `watchFolders`, blockList for `dist`/`archive`/`examples`/`coverage` only under `frontend/` (`metro.config.js`).
- Assets: keep native icons as PNG; in-app login logo is `assets/images/logo.webp`. Receipt font: `OCRA-B` only.

## Setup

### Prerequisites

- Node.js 20+ (LTS recommended)
- npm (repo uses `package-lock.json`)
- Backend API running locally (default `http://localhost:5184`) — see repo root docs
- Optional: Android Studio / Xcode / Expo Go for device runs

### Install

```bash
cd frontend
cp .env.example .env   # if you do not already have .env
npm install --legacy-peer-deps
```

`--legacy-peer-deps` may be required while TypeScript 7 is ahead of `ts-jest`’s peer range (`typescript < 7`).

### Environment

Copy from `.env.example`. Common keys:

| Variable | Purpose |
| -------- | ------- |
| `EXPO_PUBLIC_API_BASE_URL` | API base including `/api` (e.g. `http://localhost:5184/api`) |
| `EXPO_PUBLIC_DEV_TENANT_ID` | Dev tenant slug (`dev` by default in `__DEV__`) |
| `EXPO_PUBLIC_ADMIN_BASE_URL` | Optional FA base for “extend license” deep links |

Restart Metro after changing `.env`.

### Run

```bash
npm run dev               # preferred alias (same as start)
npm start                 # Expo Dev Tools + Metro (max-workers=2)
npm run android           # Android
npm run ios               # iOS
npm run web               # Web
npm run build             # Static export (`expo export`)
npm run start:clean       # start with --clear
npm start -- --reset-cache
```

From repo root: `npm run dev:pos`, `npm run test:pos`, `npm run build:pos`.

Dev URLs (typical):

```text
POS UI:  http://localhost:8081
API:     http://localhost:5184
FA UI:   http://admin.regkasse.local:3000
```

### Quality scripts

```bash
npm run typecheck         # tsc --noEmit
npm run lint              # ESLint flat config
npm run test              # Jest (jest-expo)
npm run test:contract     # payment / register gate contract suite
npm run i18n:ci           # translation validate + boundary + usage budgets
```

### Native (CNG)

`android/` and `ios/` are **not** committed. Generate when needed:

```bash
npx expo prebuild --clean
```

After changing `app.json` plugins / icons / splash / build properties, re-run prebuild before EAS or local native builds.

## Testing

Jest uses **`jest-expo`** (required for React Native + `@testing-library/react-native`). RNTL v14: `render` / `fireEvent` / `act` are **async** — always `await` them.

```bash
# Full suite
npm test

# Watch / coverage / debug
npm run test:watch
npm run test:coverage
npm run test:debug

# High-value payment / register contracts
npm run test:contract

# Single file
npx jest __tests__/expoRouterStructure.contract.test.ts
```

Conventions:

- Prefer testing-library queries over implementation details.
- Mock `utils/storage` / `sessionManager` / `apiClient` at module boundaries; do not invent parallel offline queues.
- Fiscal / payment tests must not assert on voucher plaintext in storage.

## Troubleshooting

| Symptom | Likely cause | Fix |
| ------- | ------------ | --- |
| `Unable to resolve "react-native-web/dist/..."` | Metro `blockList` matching any `dist` folder | Keep blockList **project-root-only** (`metro.config.js`); never `/dist/` globally |
| Stale bundle / odd Babel errors after config change | Metro cache | `npm start -- --reset-cache` or `npm run start:clean` |
| `npm install` fails on `ts-jest` + TypeScript 7 | Peer dependency conflict | `npm install --legacy-peer-deps` |
| Type errors about missing `@types/*` | TS 7 default `types: []` | `tsconfig.json` keeps `"types": ["*"]` |
| Camera / splash / cleartext not applied on device | Config plugin change without native regen | `npx expo prebuild --clean`, then rebuild |
| Dev API calls hit wrong tenant | Missing `EXPO_PUBLIC_DEV_TENANT_ID` / switcher | Set env or use `DevTenantSwitcher` (`__DEV__` only) |
| Login works on web but not device | Device cannot reach `localhost` API | Use LAN IP in `EXPO_PUBLIC_API_BASE_URL`; Android cleartext already enabled via `expo-build-properties` |
| Duplicate Reanimated / worklets transform errors | Manual Babel plugin + preset | Do not add `react-native-reanimated/plugin` in `babel.config.js` — Expo preset injects worklets/reanimated |
| Assets missing after cleanup | Wrong path | Login logo: `assets/images/logo.webp`; icons/splash: `icon.png` / `adaptive-icon.png`; font: `assets/fonts/OCRA-B.ttf` |

## Contributing

1. Read [`AGENTS.md`](../AGENTS.md) and [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) before fiscal, payment, TSE/RKSV, or tenant changes.
2. Prefer **small, reversible** diffs. Do not cross POS (`/api/pos/*`) and Admin (`/api/admin/*`) API boundaries.
3. **POS UI copy is German (de-DE)** — do not translate cashier-facing strings to English/Turkish.
4. Before opening a PR for this package:

   ```bash
   npm run typecheck
   npm run lint
   npm test
   ```

   Run `npm run test:contract` when touching payments, register gates, or tax normalization.
5. Do not commit secrets (`.env`, tokens). Do not commit generated `android/` / `ios/`.
6. Keep Zustand limited to ephemeral UI (`stores/posCheckoutUiStore.ts`). Never store JWTs, voucher codes, or payment payloads in Zustand.
7. After OpenAPI / shared contract changes, follow repo scripts from the monorepo root (FA client generation is separate — POS uses hand-written `services/api/*`).

## Production hosts (Single POS UI)

All tenants share one POS deployment. Tenant comes from the JWT after login — **not** from a per-tenant POS subdomain.

```text
POS UI:  https://pos.regkasse.at
FA UI:   https://admin.regkasse.at
API:     https://api.regkasse.at
```

Detail: [`docs/POS_PRODUCTION_ARCHITECTURE.md`](../docs/POS_PRODUCTION_ARCHITECTURE.md).

---

## Client state (Zustand and related)

Zustand (`zustand` ^5) is used **narrowly** for ephemeral checkout UI only. It must never hold secrets or fiscal payloads.

| Concern | Where it lives | Persisted? |
| ------- | -------------- | ---------- |
| Checkout payment-method chip + submit flag | `stores/posCheckoutUiStore.ts` (Zustand) | No (memory only) |
| Cart lines / table carts | `contexts/CartContext.tsx` | Yes — non-sensitive UI snapshot via `utils/storage` |
| Theme | `contexts/ThemeContext.tsx` | Yes — `storage` (`themeMode`) |
| Language | `i18n/` + `i18n/languageStorage.ts` | Yes — `storage` (`user-language`) |
| Auth tokens / user / tenant bootstrap | `services/session/sessionManager.ts` + `services/secureStorage.ts` | Yes — `expo-secure-store` on native |
| Offline payment/order queues | `services/payment/*`, `services/offline/*` | Yes — `storage` / IndexedDB; **no** voucher plaintext |
| Catalog / payments / receipts (server) | `services/api/*` + feature hooks / contexts | Fetched via `apiClient`; **not** in Zustand |

### Zustand → React Query (TanStack Query) — audit result

**No migration performed.** Inventory (2026-07-21):

| Store | Server data? | Action |
| ----- | ------------ | ------ |
| `stores/posCheckoutUiStore.ts` | No — local UI flags only | **Keep** as Zustand (wrong fit for React Query) |
| `stores/useCartStore.ts` | N/A | Already removed earlier (was a `CartContext` re-export) |

- `@tanstack/react-query` is **not** a dependency of this POS app today.
- Server state already lives outside Zustand (`AuthContext`, `CartContext`, `apiClient` services). Moving auth/cart/payment into React Query would be a separate, high-risk initiative — not a Zustand cleanup.
- Do **not** put checkout UI chips into React Query caches; that is client UI state.

If React Query is introduced later for catalog reads, add `QueryClientProvider` in the root layout with conservative defaults (e.g. `staleTime: 30_000`, `gcTime: 5 * 60_000`) and start with read-only product/category queries — never payment mutations as cache-as-truth.

### Zustand rules (POS)

1. **Only** short-lived UI flags that do not need SecureStore or long-lived persistence.
2. **Do not** put JWT, refresh tokens, user profiles, card data, voucher codes, TSE signatures, or full payment request bodies in Zustand.
3. Prefer **selectors** so components re-render only when their slice changes:

```ts
import {
  usePosCheckoutUiStore,
  selectSelectedPaymentMethodType,
  posCheckoutUiActions,
} from '@/stores/posCheckoutUiStore';

const method = usePosCheckoutUiStore(selectSelectedPaymentMethodType);
// Call actions without subscribing:
posCheckoutUiActions.setSelectedPaymentMethodType('cash');
```

4. Cart uses `useCart()` from `CartContext` — not Zustand.

Primary Zustand consumer today: `components/PaymentModal.tsx`.

## Camera / QR scanning (expo-camera)

| Item | Detail |
| ---- | ------ |
| Version | `expo-camera` ~56.0.8 (Expo SDK 56) |
| Plugin | `app.json` → `barcodeScannerEnabled: true`, German `cameraPermission`, `microphonePermission: false` |
| Shared permission | `hooks/usePosCameraPermission.ts` (prompt / blocked → settings) |
| Barcode presets | `constants/posCameraScan.ts` — QR-only for voucher/customer; product set for generic modal |
| Scanners | `VoucherScanner`, `QrCustomerScanner`, `BarcodeScannerModal` |
| Detector | Platform barcode pipeline inside expo-camera (not a separate `barcode-detector` package) |

Optimizations: narrow `barcodeTypes`, pause with `active` while validating, unmount session when modal hidden, `onMountError` fallback UI.

Device test: run on a physical iOS/Android device (simulators often lack a real camera). After plugin changes: `npx expo prebuild --clean` before native builds.

## Receipt print / share (expo-print + expo-sharing)

| Item | Detail |
| ---- | ------ |
| Versions | `expo-print` ~56.0.4, `expo-sharing` ~56.0.22 (Expo SDK 56 — keep via `npx expo install expo-print expo-sharing`) |
| HTML receipts | `services/receiptFormatter.ts` → thermal layout (`max-width: 300px`), viewport meta, escaped text, RKSV QR as `data:image/png;base64,…` |
| Print entry | `services/receiptPrinter.ts` → `utils/expoPrintShare.printHtmlAsync` (native print preview; PDF fallback if HTML print fails) |
| Tagesabschluss | `utils/dailyClosingReportPrint.ts` (HTML + server PDF URI) |
| PDF share | `shareDocumentAsync` (`isAvailableAsync` + share sheet); used from PaymentModal / invoices |
| Cancel vs error | iOS dismiss of print UI → `PrintCancelledError` (soft success). Real printer/share faults surface German alerts / `print_error` UI |

Device smoke (physical tablet recommended):

1. Complete a payment → print preview opens → print or dismiss (dismiss must not show “Druck fehlgeschlagen”).
2. Retry print from `print_error` UI if a real fault was forced (no printer).
3. Share Beleg/Rechnung PDF → system share sheet; confirm unavailable path on locked-down profiles if possible.

## Native build properties (`expo-build-properties`)

| Item | Detail |
| ---- | ------ |
| Version | `expo-build-properties` ~56.0.23 (Expo SDK 56 — keep via `npx expo install expo-build-properties`; do **not** jump to 57.x while on SDK 56) |
| Plugin | `app.json` → `plugins[]` entry for `expo-build-properties` |
| Android cleartext | `usesCleartextTraffic: true` — required for LAN/`http://` API bases in development and optional on-device API IP (`ApiSettingsModal`) |
| Android SDK pins | `minSdkVersion` 24, `compileSdkVersion` / `targetSdkVersion` 36, `buildToolsVersion` `36.0.0` (Expo SDK 56 defaults) |
| iOS | `deploymentTarget` `16.4`; ATS cleartext for localhost/LAN also in `ios.infoPlist.NSAppTransportSecurity` |
| EAS | `eas.json` production Android APK (`distribution: internal`) |
| Runtime | `runtimeVersion.policy: sdkVersion` in `app.json` |

After changing this plugin: `npx expo prebuild --clean`, then native build (`eas build` or local Gradle/Xcode). JS-only Metro reload does **not** apply cleartext / SDK pins.

## API client (axios)

Shared client: `services/api/config.ts` (`apiClient` / `axiosInstance`). Reliability helpers: `services/api/axiosReliability.ts`.

| Behavior | Detail |
| -------- | ------ |
| Version | `axios` ^1.18.x (keep aligned via `npx expo install axios` when upgrading Expo) |
| Auth | Request interceptor attaches `Authorization: Bearer …` from `sessionManager` when token is present and not expired; anonymous paths never get a Bearer |
| Tenant | Dev/local: `X-Tenant-Id` via `addTenantHeader` / `resolveTenantFetchHeaders` |
| CSRF | Mutations get CSRF headers when enabled (`csrf.ts`) |
| 401 | Refresh once (`/auth/refresh`), else `AUTH_SESSION_EXPIRED` |
| 403 / 5xx | Logged safely in `__DEV__` only (status + URL + message — **no** bodies/tokens) |
| Network retry | Exponential backoff (300ms × 2ⁿ), max 2 retries, **GET/HEAD/OPTIONS only** — never auto-retry payment/auth/RKSV POSTs |
| Cancellation | Pass `{ signal }` from `createApiAbortController()` / `AbortController` into `apiClient.*` |

```ts
import { apiClient, createApiAbortController } from '@/services/api/config';

const ac = createApiAbortController();
void apiClient.get('/pos/products/active', { signal: ac.signal });
// later: ac.abort();
```

## Authentication

### POS Login Credentials

Users can log in using either:

- **Username** (short identifier like `cashier1`, `manager2`)
- **Email address** (full email)

The login field accepts both formats. Usernames are generated automatically when users are created via **Schnell anlegen** in the admin panel.

### POS Login (technical)

**Login flow:**

1. Cashier enters email **or** username in the login field (`frontend/app/(auth)/login.tsx`).
2. App calls `POST /api/Auth/login` with `loginIdentifier` (and legacy `email` for compatibility) plus `clientApp: "pos"` (`frontend/services/api/authService.ts`, `contexts/AuthContext.tsx`).
3. Backend resolves the user by email, then by username, validates password and POS role policy.
4. On success, JWT (+ optional refresh token) is returned; session stores token, user, and tenant bootstrap.

**Examples:**

- Username: `cashier1` + password
- Email: `cashier@dev.regkasse.at` + password

**Username generation (Admin only):**

- When operators create users via FA **Schnell anlegen**, usernames are auto-generated (`manager1`, `cashier2`, …).
- Pattern: `{rolePrefix}{incrementalNumber}` — see `REGKASSE_AI_ONBOARDING.md` § Authentication.
- Custom usernames can be set on manual admin user create (`userName` on `POST /api/admin/tenants/{tenantId}/users`).

**Case-insensitive usernames:** `Mustafa`, `mustafa`, and `MUSTAFA` are the same account at login (backend `NormalizedUserName`). See `REGKASSE_AI_ONBOARDING.md` (Authentication).

**Persistence:** last login identifier saved as `lastUsername` and `savedLoginIdentifier` in device storage (legacy `savedUsername` still read once). On next open, the password field is focused when a saved identifier exists.

**Verified in code (no POS change required):**

| Layer | Behavior |
| ----- | -------- |
| UI | `login.tsx` — single field `loginIdentifier` (email or username) |
| Context | `AuthContext.login()` → `buildLoginPayload(..., 'pos')` |
| API client | `POST /api/Auth/login` body: `loginIdentifier`, mirrored `email`, `clientApp: "pos"` |
| Backend | `FindByEmailAsync` then `FindByNameAsync` on `LoginModel.ResolveLoginIdentifier()` |

**Automated tests:**

- POS payload: `frontend/__tests__/authService.buildLoginPayload.test.ts` (`services/api/loginPayload.ts`)
- Backend username: `AuthControllerTests.Login_WithLoginIdentifierUsername_Succeeds`, `Login_WithLoginIdentifierUsername_ClientAppPos_Succeeds`

**Manual smoke test (dev):**

1. In FA, create or quick-create a tenant user with username `cashier1` (Cashier role, active).
2. Set `EXPO_PUBLIC_DEV_TENANT_ID` / API base URL so POS hits the same tenant as the user.
3. Open POS login, enter `cashier1` + password (not the full email).
4. Expect successful login and cashier home; wrong password → German error via `auth` i18n.

Contract detail: [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) § `POST /api/Auth/login`.

Repo-wide auth detail: `REGKASSE_AI_ONBOARDING.md` (Authentication).

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- **Production (POS):** shared host `pos.regkasse.at` — tenant from JWT `tenant_id` after login (not from Host slug).
- **Production (API):** `api.regkasse.at` — authenticated traffic scoped by JWT `tenant_id`.
- **Development:** `X-Tenant-Id` header or `?tenant={slug}` (`EXPO_PUBLIC_DEV_TENANT_ID` + `DevTenantSwitcher`).
- Super Admin is an admin-panel concern (`admin.regkasse.at`), not the POS app.
- Reserved labels (never tenant slugs): `pos`, `api`, `admin`, `www`.

### Data Isolation

- All POS API data is scoped server-side by tenant; clients must target the correct API and authenticated tenant.
- Cross-tenant IDs are not visible to other tenants (backend returns **404**, not 403).

### Development Mode

- Localhost API: send `X-Tenant-Id: <slug>` on requests (`services/tenant/tenantStorage.ts`, constant `TENANT_HTTP_HEADER`)
- Alternative: `?tenant=<slug>` on API URLs (Development backend only)

## Development Setup for Multi-Tenant Testing

### Option 1: Header-based (simplest)

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

### Option 2: Query string

```bash
curl "http://localhost:5184/api/health?tenant=dev"
```

### Option 3: Hosts file (optional local labels)

```text
127.0.0.1 admin.regkasse.local
127.0.0.1 dev.regkasse.local
```

Then use header/query on `localhost:5184`, or FA at `http://admin.regkasse.local:3000`.

### Dev tenant override (POS)

- `EXPO_PUBLIC_DEV_TENANT_ID` — default slug when unset in `__DEV__`
- `services/tenant/tenantStorage.ts` — sends `X-Tenant-Id` on loopback API calls in Development
- Effective slug order: dev switcher storage → env var → login/license bootstrap

### Option 4: POS UI

`DevTenantSwitcher` in tab layout (`__DEV__` only). Loads tenants from `GET /api/tenants/switcher` when authenticated (same as FA dev header switcher).

## POS Tenant Configuration

### Production

- After login / license bootstrap: `tenant_id`, `tenant_slug`, and API base from session (`tenantStorage.ts`).
- Shared POS UI talks to `https://api.regkasse.at` (or configured base); tenant boundary is the JWT.

### Development

```env
EXPO_PUBLIC_DEV_TENANT_ID=dev
```

POS adds `X-Tenant-Id` and optional `?tenant=` on the API base URL automatically (`services/api/config.ts`, `services/tenant/devTenant.ts`).

Full guide: `REGKASSE_AI_ONBOARDING.md`.

### POS responsibilities

- Do not cache or replay offline payment payloads across tenants
- After login, respect `tenantId` / `tenantSlug` from auth and license bootstrap (`contexts/AuthContext.tsx`, `api/license.ts`)
- Never invent a second offline queue or bypass RKSV/TSE gates

## API Headers

### Tenant Identification

- **Production:** JWT `tenant_id` on shared API host.
- **Development:** `X-Tenant-Id: {slug}` or `?tenant={slug}`.

## Native Android / iOS workflow (CNG)

This project uses **Continuous Native Generation (prebuild-only)**. The `android/` and `ios/` folders are **not** committed — they are generated from `app.json` when needed.

| Task | Command |
| ---- | ------- |
| Generate native projects | `cd frontend && npx expo prebuild` |
| Clean regenerate | `cd frontend && npx expo prebuild --clean` |
| Run on Android (Expo Go / dev client) | `npm run android` |

After changing `plugins`, `android`, `ios`, splash, or icon entries in `app.json`, run **`npx expo prebuild --clean`** locally before native builds or EAS Build.

Native folders are listed in `frontend/.gitignore`.

## Deployment Requirements

- Production POS: `https://pos.regkasse.at` → API `https://api.regkasse.at`.
- Wildcard DNS / TLS and reserved host labels are infrastructure concerns — see `REGKASSE_AI_ONBOARDING.md` and [`docs/POS_PRODUCTION_ARCHITECTURE.md`](../docs/POS_PRODUCTION_ARCHITECTURE.md).

Repo-wide detail: `REGKASSE_AI_ONBOARDING.md` (Multi-Tenant Architecture, API Headers, Deployment).

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
