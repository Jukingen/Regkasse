# 05 — POS App (`frontend/`, package `cash-register`)

> Part 5 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). The POS is a sales surface. The backend is always the final authority for fiscal decisions.

## 1. Overview

- Expo SDK 58 (`expo ^58.0.0`), React Native 0.88.1, React 19.3.0, TypeScript ~6.0.3, `expo-router ~58` (file-based routes in `frontend/app/`).
- Runs on native (Android/iOS via EAS builds) and web (`expo start --web`, `react-native-web`). Dev URL: `http://localhost:8081`.
- UI language: **German (de-DE)**. Do not translate POS UI text to English or Turkish. Note that `frontend/i18n/locales/` also contains `en.json` and `tr.json` plus per-namespace `de/*.json` folders, so the tooling supports more languages. The product rule still says German operator UI.
- State: React Contexts (auth, cart, license, maintenance, TSE health, register readiness, vertical profile, theme), a few Zustand stores (`stores/onlinePaymentStore.ts`, `stores/posCheckoutUiStore.ts`), local storage helpers. Network: axios. Validation: zod 4.
- Monitoring: `@sentry/react-native` (`src/monitoring/initPosSentry`).

Commands (from repo root or `frontend/`):

```bash
npm run dev:pos            # root;  or: cd frontend && npm run dev   (expo start)
cd frontend && npm run typecheck
cd frontend && npm run lint
cd frontend && npm run test          # jest (jest-expo), about 166 test files
cd frontend && npm run test:contract # payment contract subset
cd frontend && npm run test:e2e      # Playwright
cd frontend && npm run i18n:ci       # translation hard gate
```

Environment (`frontend/.env`, see `.env.example`):

```env
EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api
EXPO_PUBLIC_DEV_TENANT_ID=dev
```

Restart Metro after changing `.env`. In `__DEV__` the dev tenant order is: `DevTenantSwitcher` / stored `dev_tenant_id` → `EXPO_PUBLIC_DEV_TENANT_ID` (default `dev`) → persisted login/license slug. Production uses the JWT `tenant_id` only.

## 2. Route structure (`frontend/app/`)

| Group | Files |
|---|---|
| Root | `_layout.tsx` (providers, Sentry, splash, fonts, i18n ready), `index.tsx`, `payment-result.tsx`, `order-tracker.tsx`, `+html.tsx` (web document) |
| `(auth)/` | `login`, `change-password`, `license-expired`, `onboarding` |
| `(tabs)/` (main POS shell) | `cart`, `cash-register`, `payment`, `receipt-list`, `orders`, `kitchen-display`, `rooms` (lodging), `appointments`, `patient-record` (vet), `ticket-validate`, `fiskaly-operations`, `settings`, `admin-menu` |
| `(screens)/` | `invoices`, `reports`, `offline-queue`, `license-activate`, `cash-register-select`, `SplitScreen`, `PaymentHistoryScreen`, `kitchen-display`, `rooms`, `appointment`, `patient-record`, `admin-menu`, plus `taskmaster`, `simple-todo`, `task-suggestions` (look like auxiliary/dev screens; not verified) |
| `customer/` | Customer-facing portal: `index`, `dashboard` (public customer dashboard by phone + order number) |
| `payment/`, `online-payment/` | `result`, `callback` for hosted card/PayPal checkout |
| `tenant/[slug]` | Tenant entry route |

`(tabs)/_layout.tsx` is the POS shell. It composes: register readiness and TSE health providers, banners (offline, TSE status/offline restriction, time sync, license expiry/warning, grace period, maintenance, Startbeleg required, Tagesabschluss reminder, Monatsbeleg badge), the `PaymentModal`, an offline queue indicator and limit modal, the kitchen hub bridge, dev tenant switcher (dev only), and role guard `isPosAllowedRole`. The cashier flow is cart → payment modal → receipt.

## 3. Code layout

| Folder | Role |
|---|---|
| `services/api/` | One module per backend area (about 60 files): `authService`, `cartService`, `paymentService`, `posPaymentPaths`, `receiptService`, `cashRegisterService`, `tagesabschlussService`, `rksvSpecialReceiptsService`, `tseService`/`tseHealthApi`, `finanzOnlineService`, `productService`, `variantService`, `imeiService`, `lodgingService`, `ticketService`, `appointmentService`, `kitchenOrderService`, `splitService`, `shiftService`, `licenseStatusService`, `maintenanceService`, `posStatusOverviewService`, `posCashRegisterReadinessService`, `publicCustomerDashboardService`, ... |
| `services/api/apiPaths.ts` | **Single source for endpoint paths.** Comment in the file: paths "MUST match swagger.json / backend route attributes exactly"; never hardcode endpoint strings elsewhere. Canonical POS surface is under `/pos/*` |
| `services/api/config.ts` | Axios base URL, tenant header injection (`X-Tenant-Id` in dev), `hydrateDevTenantApiBaseUrl()` |
| `services/offline/` | `offlineStorage`, `offlineSyncService`, `offlineQueueSnapshot`, `offlineOrderManager`, `offlineTenantLimit`, `retryOfflineQueues` |
| `services/payment/` | `pendingPaymentQueue`, `onlinePaymentFlow`, `onlinePaymentMethods`, `openOnlinePaymentHostedPage`, `parsePaymentResultParams` |
| `services/cart/` | `pendingCartMutationQueue`, `cartMutationSyncNotifier` |
| `services/printing`, `receiptPrinter.ts`, `receiptFormatter.ts`, `receiptTemplate.ts`, `PrinterService.ts`, `ticketPrinter.ts` | Receipt/ticket formatting and printing (`expo-print`) |
| `services/auth`, `session`, `shift`, `license`, `tenant`, `verticalProfiles`, `appUpdate`, `config`, `customerApp`, `linking`, `pos` | Cross-cutting services (tokens in `secureStorage.ts`, tenant storage, license bootstrap, updates, deep links) |
| `contexts/` | `AuthContext`, `CartContext`, `LicenseContext` / `LicenseStatusContext` / `MandantLicenseWarningContext`, `MaintenanceContext`, `PosRegisterReadinessContext`, `PosStatusOverviewContext`, `TseHealthContext`, `VerticalProfileContext`, `DevelopmentModeContext`, `TableSlotContext`, `TaxiTripContext`, `ImeiSelectionContext`, `MobileServiceJobContext`, `ThemeContext`, `SystemContext`, `AppStateContext` |
| `hooks/` | `useCart*`, `useCashRegister*`, `usePayment*`, `usePermission` / `usePosPermissions`, `useOfflineStatus`, `useOfflineOrderManager`, `useKitchenHub`, `useMonatsbelegStatus`, `useLicenseStatus`, `useConnectivity`, `useIdleTimeout`, ... |
| `components/`, `src/components/` | UI components (`PaymentModal`, banners, `ToastNotification`, lodging, soft theme, dev tools) |
| `features/auth`, `features/payment`, `src/features/*` | Feature code (license, admin-navigation) |
| `i18n/` | i18next setup, `locales/de|en|tr` (JSON per namespace, plus `de.json`, `en.json`, `tr.json` at the top level) |
| `constants/`, `utils/`, `types/` | Config, polling intervals, feature flags, formatting, guards (`posRoleGuard`, `posRegisterGateCopy`, `posCashRegister`) |
| `__tests__/`, `tests/`, `e2e/` | Jest tests (including `contract/` and `integration/`), Playwright e2e |

## 4. Behavioral rules the POS must respect

- **API boundaries:** call `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` (and the shared endpoints the existing services already use, such as `/api/rksv/*`, `/api/offline-transactions/*`, `/api/license/*`). Never call `/api/admin/*`. Add new paths to `apiPaths.ts` and check them against `swagger.json`.
- **Backend is final authority.** POS guards are UX only:
  - Decommissioned register: block payment (`POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE`).
  - Startbeleg required: block sales/session start until resolved (`StartbelegRequiredBanner`).
  - Monatsbeleg required: blocking modal/gate where implemented.
  - TSE unavailable: block or fail the fiscal payment per policy; show status banners.
  - Offline + voucher: block immediately. Never enqueue voucher payloads.
  - Time-sync problems: banner (`TimeSyncBanner`); the backend rejects online fiscal payments when the NTP gate fails.
- **Offline:** local queue key `@regkasse/offline_transactions_v1` (AsyncStorage/localStorage). `NON_FISCAL_PENDING` items replay through `POST /api/offline-transactions/replay`. Queue survives restarts. Per-tenant cap 50 (HTTP 409 `LIMIT_EXCEEDED`), warning at 80%. Full order snapshots use `/api/pos/offline-orders/*` (a different system). Never store voucher codes locally.
- **Working hours** never gate the POS (`posOperationsAllowed` is always true).
- **Vertical profiles:** `VerticalProfileContext` / `useVerticalFeatures` drive which tabs and fields appear (tables, kitchen display, appointments, patient record, IMEI, taxi, tickets, rooms, retail barcode/variants).
- **Idle timeout** and session policy: `useIdleTimeout`, `sessionPolicyService`.
- **Logout:** clears SecureStore and offline queues (`docs/AUTH_LOGOUT.md`).
- **Tokens:** Bearer tokens from SecureStore on native. Do not log tokens or voucher codes.
- **Printing:** `receiptFormatter` / `receiptTemplate` render the fiscal receipt content that the backend returns (QR payload, signature, RKSV header from `/api/pos/company`). Do not change fiscal content on the client. Reprint (`/api/pos/receipts/{id}/reprint`) creates no new fiscal document.

## 5. Typical flows

1. **Login:** `(auth)/login` → `authService.buildLoginPayload` sends `loginIdentifier`, `password`, `clientApp: 'pos'` → tokens stored in SecureStore → license status/bootstrap → cash register selection (`cash-register-select`) → `(tabs)`.
2. **Sale:** add items (`cartService`, `/api/pos/cart/*`) → choose payment method (`/api/pos/payment/methods`) → `PaymentModal` → `POST /api/pos/payment` with an idempotency key → receipt data/QR → print or show. For card/PayPal: initiate hosted checkout (`/api/pos/payment/initiate`), poll the status, then fiscal `POST /api/pos/payment`.
3. **Offline:** payment fails with connectivity or TSE trouble → intent queued → banner/indicator → replay after recovery.
4. **Closing:** Tagesabschluss via `tagesabschlussService` (`/api/Tagesabschluss/*`), reminders via `TagesabschlussReminder`.
5. **Kitchen:** SignalR `KitchenHub` (`kitchenHubClient`, `useKitchenHub`) updates the kitchen display and pending count.

## 6. Tests and quality gates

- Jest with `jest-expo`; `@testing-library/react-native`. About 166 test files; contract tests for payment (`npm run test:contract`) protect the POS ↔ backend payment contract.
- Playwright e2e under `frontend/e2e` and `frontend/tests/e2e`.
- CI: `frontend-ci.yml` (lint, typecheck, test, i18n on pull requests) and `frontend-eas-build.yml` (manual EAS builds: universal or "Gastronomie" listing; no store submit).
- ESLint `eslint-config-universe`, Prettier. No `console.log` in production code. No TypeScript `any` (use `unknown`).

## 7. Checklist for a POS change

1. Is the string user-facing? Put it in the German locale file(s) and keep other locale files consistent (`npm run i18n:ci`).
2. New endpoint? Add to `apiPaths.ts`, a service module, and a test. Confirm the route exists in `backend/swagger.json`.
3. Touches payment, cart, offline, or receipt code? That is high risk. Plan first. Extend contract tests.
4. Does it depend on a vertical profile? Use `useVerticalFeatures`, not hardcoded tenant checks.
5. Never bypass backend guards or store secrets locally.
