# Swagger / OpenAPI guardrails

## Host wiring

Swagger is registered in **`ApplicationHost`** (not `Program.cs` — that file only boots the host).

| Surface | URL (Development) |
|---------|-------------------|
| Swagger UI | `http://localhost:5184/swagger` |
| OpenAPI JSON | `http://localhost:5184/swagger/v1/swagger.json` |

## Inclusion rules

1. **Default:** all `[ApiController]` actions appear in document `v1`.
2. **Legacy aliases hidden** via `LegacySwaggerPathExclusions` + `DocInclusionPredicate` (`api/Cart`, `api/Payment`, `api/Product`, …). Runtime may still serve them.
3. **Dev/test noise hidden** with `[ApiExplorerSettings(IgnoreApi = true)]`:
   - `TestController`
   - `DevCleanupController`
   - `FinanzOnlineDevTestController`

Do **not** put `[ApiExplorerSettings(IgnoreApi = true)]` on production POS/Admin/Auth controllers.

## ProducesResponseType

Prefer explicit `[ProducesResponseType]` on public Auth/Health and contract-critical Admin/POS actions so Swagger status codes match runtime.

## Regenerate committed contract

```bash
node scripts/generate-backend-openapi.mjs
node scripts/validate-critical-openapi-paths.mjs
```
