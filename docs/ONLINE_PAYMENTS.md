# POS online payments (card / PayPal)

> **Scope:** Hosted POS checkout (Kreditkarte / PayPal) via a payment gateway. Rows live in `gateway_payment_intents` (`CardPaymentTransaction`) **before** fiscal commit.  
> **Not in scope:** Customer website/app **online orders** (`online_orders` — see [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md)), TSE signing, RKSV receipts. Fiscal Beleg creation stays on `POST /api/pos/payment`.

**Last updated:** 2026-08-26

**Related:** [`../backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) · [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) · [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md) · [`AGENTS.md`](../AGENTS.md) § API Boundaries · § High-risk flows

---

## Overview

Cashiers take **card or PayPal** on the POS terminal. The acquirer captures money first; the cash register then creates the RKSV receipt. A webhook never writes `payment_details`.

| Surface | Who | Purpose |
|---------|-----|---------|
| POS (`frontend`) | Cashier | Initiate hosted checkout, poll status, then fiscal `POST /api/pos/payment` |
| API | POS JWT | `/api/pos/payment/initiate`, `/api/pos/payment/initiate/{id}` (alias `/online/{id}`) |
| Webhooks | Stripe / Mock / PayPal | Signature-verified status updates only |
| FA **Online-Zahlungen** | Super Admin (`online-payments.manage`) | List intents, inspect details, synthetic test webhooks |

Do **not** treat a gateway intent as a Beleg. Fiscal **COMPLETED** is the POS link of `payment_id` on `POST /api/pos/payment`, not a webhook target.

### Flow

```text
POS initiate
  → POST /api/pos/payment/initiate  (cashRegisterId, amount, method, idempotencyKey)
  → persist gateway_payment_intents  (status Pending / DTO AWAITING_PAYMENT_GATEWAY)
  → hosted redirect or client secret

Customer pays at the provider
  → POST /api/webhooks/payment/{stripe|mock|paypal}
  → status Succeeded  (DTO GATEWAY_SUCCEEDED); PaymentId still null

POS commits the sale
  → POST /api/pos/payment  with CardPaymentIntentId / OnlinePaymentId
  → TSE / RKSV payment_details; intent PaymentId set  (DTO COMPLETED)
```

Mixed **Gutschein + card/PayPal:** the gateway amount is the **remainder after voucher**, not cart gross. Offline POS **must not** enqueue hosted methods (no voucher codes or PAN in the offline queue).

### Status mapping

Persistence uses card-intent statuses (`Pending`, `Succeeded`, `Failed`, `Refunded`). POS/admin DTOs map them:

| Card row | DTO (`OnlinePaymentStatuses`) |
|----------|-------------------------------|
| Created / Pending | `AWAITING_PAYMENT_GATEWAY` |
| Succeeded, no `PaymentId` | `GATEWAY_SUCCEEDED` |
| Succeeded, `PaymentId` set | `COMPLETED` |
| Failed / Cancelled | `FAILED` |
| Refunded | `REFUNDED` |

Illegal transitions (for example Succeeded → Failed) are ignored; the existing row is returned.

---

## Configuration (Stripe / PayPal / Mock)

Section: `PaymentGateway` (`backend/Configuration/PaymentGatewayOptions.cs`). Env overrides use double underscores (`PaymentGateway__Stripe__ApiKey`).

| Key | Development | Production | Notes |
|-----|-------------|------------|--------|
| `Provider` | `Mock` (default) | **`Stripe`** or **`None`** | Mock is **rejected at Production startup**. `None` = cash-only (no live card intents) |
| `Stripe:ApiKey` | empty / test `sk_test_…` | `sk_live_…` (secret store) | Prefer nested key; legacy `StripeSecretKey` still resolves |
| `Stripe:WebhookSecret` | empty / `whsec_…` | `whsec_…` | Prefer nested key; legacy `StripeWebhookSecret` still resolves |
| `OrphanIntentTtlDays` | `7` | `7` | Succeeded intents with no fiscal `PaymentId` are voided after this age |
| `RequireCardIntentForPosPayments` | `false` | as required | When true, POS card sales must send a confirmed intent id |
| `SimulateDelayMs` | optional | unused | Mock network delay |

**PayPal:** POS method codes are `paypal` and `credit_card` (backend normalizes `credit_card` → `card`). There is no separate `PaymentGateway:Provider=PayPal` today. Development hosted checkout uses **Mock** (redirect URL). Production live acquirer is **Stripe** when `Provider=Stripe`. Webhook path `paypal` uses the same generic HMAC as Mock.

### Secrets (do not commit)

```env
PaymentGateway__Provider=Stripe
PaymentGateway__Stripe__ApiKey=sk_live_…
PaymentGateway__Stripe__WebhookSecret=whsec_…
```

Local JSON examples: `backend/appsettings.example.json`, `backend/appsettings.Production.example.json`. Put live keys in user-secrets or the vault, never in git.

### Stripe Dashboard

1. Create a webhook endpoint: `https://api.regkasse.at/api/webhooks/payment/stripe`.
2. Subscribe at least to `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, `charge.refunded`.
3. Copy the signing secret into `PaymentGateway:Stripe:WebhookSecret`.
4. Stripe sends `Stripe-Signature`; the API verifies with `EventUtility.ConstructEvent`.

### Mock / PayPal webhook HMAC

When a webhook secret is configured, Mock and PayPal posts must send:

```http
X-Payment-Webhook-Signature: <hex HMAC-SHA256 of raw body>
```

Header may use a `sha256=` prefix. If the secret is **empty**, Mock/PayPal payloads are accepted without HMAC (**Development / Mock only** — do not rely on this in Production).

CSRF is not applied to `/api/webhooks/*`. Tenant middleware also skips `/api/webhooks` (provider payload identifies the intent).

### Return URLs

Allowed schemes: `https`, `http`, `regkasse`, `cashregister`, `exp`, `exps`, or a relative path. Loopback hosts (`localhost`, `127.0.0.1`) are rejected **outside Development**. Defaults: native `regkasse://payment-result`, web `/payment/result`.

---

## FA Online-Zahlungen console

**UI:** `/admin/online-payments` (alias `/admin/payments/gateway-console`)  
**Permission:** `online-payments.manage` — **Super Admin only** (not Mandanten-Admin / Cashier). Missing permission → forbidden alert, not a POS 404.  
**API:** `GET /api/admin/online-payments`, `GET /api/admin/online-payments/{id}`, `POST /api/admin/online-payments/test`

### Monitoring (Transaktionen)

- Paginated list of gateway intents for the ambient tenant (cross-tenant → HTTP **404**).
- Columns: time, amount, status, mandant, method, provider.
- Detail drawer: intent id, last webhook event, error text, synthetic flag.

Synthetic test rows are tagged (description / metadata). They are **not** fiscal receipts.

### Testing (Testkonsole)

1. Open **Testkonsole**.
2. Choose amount and method (Karte / PayPal).
3. **Testzahlung senden** → `action: create` (Mock intent, no TSE).
4. **Erfolgreichen Webhook simulieren** / **Fehlgeschlagenen Webhook simulieren** → `webhookSucceeded` / `webhookFailed`.

Confirm dialogs are required before webhook simulation. Webhook buttons stay disabled until a test payment exists.

Live E2E against a real API (test mode): `E2E_LIVE=1` plus admin credentials. Default Playwright mocks `/api/admin/online-payments`.

---

## POS behavior

- Methods shown as **Kreditkarte** (`credit_card`) and **PayPal** (`paypal`).
- `POST /api/pos/payment/initiate` then poll `GET /api/pos/payment/initiate/{id}`.
- UI state is ephemeral Zustand (`onlinePaymentStore`) — no tokens, PAN, or voucher codes.
- Hosted methods are **disabled when the device is offline**; they are not written to the offline payment queue.
- After `GATEWAY_SUCCEEDED`, POS still must call fiscal `POST /api/pos/payment`.

---

## Integrity rules (G1–G6)

| Id | Rule | Behavior |
|----|------|----------|
| G1 | Mixed payments | Card/PayPal intent amount = remainder after Gutschein, not cart gross |
| G2 | Idempotency | Unique `(TenantId, IdempotencyKey)` on `gateway_payment_intents`; replay returns the existing row and does not call the provider again |
| G3 | Webhook | Verify signature; apply status only; **never** insert `payment_details`; duplicate `eventId` is a no-op |
| G4 | Refund | Acquirer refund on a captured intent; no fiscal storno row from the gateway service |
| G5 | Orphan cleanup | Daily job voids **Succeeded** intents with `PaymentId == null` older than `OrphanIntentTtlDays` (gateway refund → `Refunded`) |
| G6 | Isolation / URLs | Cross-tenant get → HTTP **404**; decommissioned register blocked; return URL loopback rejected outside Development |

Hosted service: `OrphanIntentCleanupService` (24h interval, `IServiceScopeFactory` + scoped EF — no root `DbContext`).

---

## API cheat sheet

| Method | Path | Auth |
|--------|------|------|
| POST | `/api/pos/payment/initiate` | POS JWT + `payment.take` |
| GET | `/api/pos/payment/initiate/{id}` | POS JWT + `payment.take` |
| GET | `/api/pos/payment/online/{id}` | Same handler (alias) |
| POST | `/api/pos/payment` | Fiscal commit (existing POS payment) |
| POST | `/api/webhooks/payment/{provider}` | Public + signature |
| GET | `/api/admin/online-payments` | Super Admin + `online-payments.manage` |
| POST | `/api/admin/online-payments/test` | Super Admin + `online-payments.manage` |

OpenAPI: `backend/swagger.json`. Contract tests: `OnlinePaymentContractTests`, `scripts/validate-critical-openapi-paths.mjs`.

---

## Troubleshooting

### Webhook rejected (`ONLINE_PAYMENT_INVALID_WEBHOOK`)

- Stripe: missing or wrong `Stripe-Signature` / `WebhookSecret` mismatch (test vs live).
- Mock/PayPal: `X-Payment-Webhook-Signature` does not match HMAC of the **raw** body when a secret is set.
- Unknown `{provider}` → `ONLINE_PAYMENT_UNKNOWN_PROVIDER`.
- Unknown `paymentIntentId` → HTTP 200 ignored (no row). Check `gateway_payment_intent_id`.

### Intent stays `AWAITING_PAYMENT_GATEWAY`

- Customer did not finish hosted checkout.
- Webhook never reached the API (firewall, wrong URL, Stripe endpoint still in test mode).
- Illegal transition: already terminal; later `failed` events are ignored.

### Orphan intents (captured, no Beleg)

- Cashier captured at the gateway then abandoned the POS sale.
- After `OrphanIntentTtlDays` (default 7), `OrphanIntentCleanupService` refunds at the gateway and sets `Refunded`.
- Logs: `Orphan gateway intent cleanup voided {Count} row(s).` / `Orphan intent {Id} gateway void failed`.
- Inspect in FA **Online-Zahlungen** (`Succeeded` + empty completed fiscal link / synthetic vs live).

### POS initiate fails

| Code | Typical cause |
|------|----------------|
| `ONLINE_PAYMENT_INVALID_AMOUNT` | Amount &lt; 0.01 |
| `ONLINE_PAYMENT_INVALID_METHOD` | Not `card` / `paypal` / `credit_card` |
| `ONLINE_PAYMENT_INVALID_RETURN_URL` | Bad scheme or loopback in non-Development |
| Register decommissioned | Cash register not allowed for new payments |
| `ONLINE_PAYMENT_GATEWAY_ERROR` / `CREATE_FAILED` | Provider down or declined (Mock decline amount **12.34** EUR) |

Production `Provider=Mock` prevents process start. Use `Stripe` or `None`.

### FA console empty or forbidden

- Need Super Admin + `online-payments.manage`.
- Ambient tenant required for mandant data APIs (Super Admin platform exemptions do **not** include this route).
- Test console does not create RKSV receipts; Tagesabschluss will not show synthetic rows as Belege.

---

## Tests (CI)

| Layer | Where |
|-------|--------|
| Backend unit | `PaymentGatewayServiceTests`, `OnlinePaymentAdminServiceTests`, `OnlinePaymentGapTests` — `backend-ci.yml` |
| Backend integration | `OnlinePaymentIntegrationTests` (`Category=PostgreSql`) — `backend-postgres-integration-tests.yml` |
| OpenAPI | `OnlinePaymentContractTests` — `backend-ci.yml`, `api-contract-tests.yml` |
| FA unit | `frontend-admin/src/features/online-payments/**` — `frontend-admin-ci.yml` |
| FA E2E | `frontend-admin/tests/e2e/online-payment.spec.ts` (`E2E_LIVE` optional) |
| POS | `onlinePaymentStore`, payment service, `pos-payment-contract.test.ts` — `frontend-ci.yml` / `frontend-pos-ci.yml` |
