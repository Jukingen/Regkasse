# Payment Legacy Route Deprecation Plan

## Scope

Legacy payment route family:
- `/api/Payment/*` (deprecated)

Canonical route families:
- POS: `/api/pos/payment/*`
- Admin: `/api/admin/payments/*`

This plan keeps behavior backward compatible while making usage measurable and removal date explicit.

## Inventory and canonical mapping

| Legacy route | Method | Canonical replacement | Notes |
|---|---|---|---|
| `/api/Payment/methods` | GET | `/api/pos/payment/methods` | POS methods list |
| `/api/Payment` | POST | `/api/pos/payment` | Create payment |
| `/api/Payment/{id}` | GET | `/api/pos/payment/{id}` | Payment detail |
| `/api/Payment/customer/{customerId}` | GET | `/api/pos/payment/customer/{customerId}` | Customer payments |
| `/api/Payment/method/{paymentMethod}` | GET | `/api/pos/payment/method/{paymentMethod}` | Method filtered list |
| `/api/Payment/date-range` | GET | `/api/pos/payment/date-range` | Date range |
| `/api/Payment/{id}/cancel` | POST | `/api/pos/payment/{id}/cancel` | Cancel |
| `/api/Payment/{id}/refund` | POST | `/api/pos/payment/{id}/refund` | Refund |
| `/api/Payment/statistics` | GET | `/api/pos/payment/statistics` | Stats |
| `/api/Payment/{id}/qr.png` | GET | `/api/pos/payment/{id}/qr.png` | QR PNG |
| `/api/Payment/{id}/qr.svg` | GET | `/api/pos/payment/{id}/qr.svg` | QR SVG |
| `/api/Payment/{id}/receipt` | GET | `/api/pos/payment/{id}/receipt` | Receipt payload |
| `/api/Payment/{id}/tse-signature` | POST | `/api/pos/payment/{id}/tse-signature` | Signature refresh |
| `/api/Payment/{id}/signature-debug` | GET | `/api/pos/payment/{id}/signature-debug` | RKSV/debug tooling |
| `/api/Payment/verify-signature` | POST | `/api/pos/payment/verify-signature` | Verify |

## Deprecation headers

For any request entering `/api/Payment/*`, backend returns:
- `Deprecation: true`
- `Sunset: Wed, 30 Sep 2026 23:59:59 GMT`
- `Link: </api/pos/payment/...>; rel=\"successor-version\"`
- `X-Regkasse-Canonical-Route: /api/pos/payment/...`

## Usage logging and metrics

- Structured warning log on each legacy route usage.
- Prometheus metric:
  - `legacy_payment_route_hits_total{route_template,http_method}`

Metric enables rollout gating:
- remove only after sustained low/zero hit volume and frontend migration completion.

## Frontend migration status

- POS frontend already on canonical `/api/pos/payment/*`.
- Admin frontend canonical payment UI already on `/api/admin/payments/*`.
- Admin Orval generation now strips `/api/Payment/*` so new code cannot regress to legacy client usage.

## Removal timeline

- Sprint N: headers + telemetry + inventory (this document).
- Sprint N+1: enforce canonical usage in active frontend modules, track stragglers.
- Sprint N+2: freeze legacy additions, alert on any legacy usage.
- Sprint N+3 (after sunset readiness): remove `/api/Payment/*` alias route from backend.

## Acceptance mapping

- Legacy route inventory exists: this document.
- Canonical replacement exists for each legacy route: mapping table above.
- Deprecation headers returned: implemented in `PaymentController`.
- Usage measurable: log + Prometheus counter implemented.
- Frontend migrated or tracked: migration status section and Orval governance update.
- Removal date documented: sunset + timeline section.
