# DTO conventions (`Models/DTOs` and `DTOs/`)

**Last updated:** 2026-07-21

## Serialization (source of truth)

ASP.NET Core JSON options in `ApplicationHost` use:

- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `PropertyNameCaseInsensitive = true`

Therefore **do not** blanket-add `[JsonPropertyName("camelCase")]` on every property — it duplicates the naming policy, increases noise, and risks typos (especially acronyms).

### When to use `[JsonPropertyName]`

| Case | Example |
|------|---------|
| Wire name differs from camelCase of the C# name | `requires2FA`, legacy `email` |
| Explicit contract lock for an obsolete field being phased out | `[Obsolete] + [JsonPropertyName("approvalId")]` |
| Non-MVC serializers with `PropertyNamingPolicy = null` (fiscal/DEP) | Dedicated export DTOs under RKSV modules |

## Immutability

- Prefer `{ get; init; }` for **new** request/response DTOs and when rewriting a small type.
- Keep `{ get; set; }` when existing code mutates instances after construction (list mappers, incremental builders).
- Do **not** mass-convert the entire folder in one PR.

## Obsolete / removal timeline

Unused or redundant wire fields are marked `[Obsolete("… Planned removal after 2026-12-31.")]` while still serialized (responses unchanged for clients).

| Field / type | Reason | Remove after |
|--------------|--------|--------------|
| `LoginModel.Email` | Prefer `loginIdentifier` | 2026-12-31 |
| `CancellationResponse.ApprovalId` / `RefundResponse.ApprovalId` | FA uses `requiresApproval` | 2026-12-31 |
| `PaymentListItemDto.HasVoucherRedemption` | Prefer `voucherRedeemedAmount` | 2026-12-31 |
| `TrendDataPoint.WeekNumber` | Prefer `label` / `date` | 2026-12-31 |
| `UserUsernameHistoryDto.ChangedByEmail` | Prefer `changedByUserId` | 2026-12-31 |

**Removed (2026-07-21):** dead `CancellationRequest` / `RefundRequest` (superseded by `CancelPaymentRequest` / `RefundPaymentRequest`).

**Note:** `UserSessionDto` remains the wire type for `/api/user/sessions/devices`; `ActiveSessionDto` is the richer list used by `/api/user/sessions`.

## Related surfaces

- Larger fiscal/POS contracts live under `backend/DTOs/` — same camelCase rules apply.
- OpenAPI / Orval regenerate after removing obsolete fields (not while they remain).
