# ADR 0004: Per-tenant Fiskaly credentials (BYOK)

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Fiskaly SIGN AT is the production TSE for Austrian mandants. Fiskaly organizations, API keys, and signature-creation units belong to the business that registered them. A single host API key would put every mandant in one Fiskaly organization, which breaks separation and makes a key rotation a platform-wide outage.

Development still needs a way to sign without a live organization.

## Decision

Each mandant has its own Fiskaly credentials (`tenant_fiskaly_credentials`): environment (`TEST` or `LIVE`), API key, secret, and SCU id. Signing and DEP read that row for the ambient tenant. An inactive row fails closed. Production does not fall back to host `Fiskaly:ApiKey`.

Host `Fiskaly:*` values are the Development fallback, and only when that tenant has no credential row. `Fiskaly:Environment` on the host is not the tenant's LIVE/TEST switch once a row exists.

The FA credential screen stores secrets write-only. Status responses return a hint, not the secret. The public leaf certificate on the tenant row is required for signing and DEP when the source is that row.

## Consequences

- Onboarding a mandant includes a Fiskaly organization that the mandant owns. Regkasse does not share one production organization.
- Cutover is per tenant. See [`../FISKALY_PRODUCTION_CUTOVER.md`](../FISKALY_PRODUCTION_CUTOVER.md).
- Host keys in Production do not repair a missing tenant row.
- Logs and exports must not print the API secret or the JWS. The operational TSE export is metadata only (ADR 0002).
- Germany's fiscal provider choice is a different decision and does not reuse this Austrian key row.
