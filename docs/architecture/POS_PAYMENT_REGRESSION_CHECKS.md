# POS payment — regression checks

Run from repo root after changing POS payment code.

```bash
# Removed two-step flow must stay gone
rg "/Payment/initiate|/Payment/confirm" frontend --glob "*.{ts,tsx}"

# New POS HTTP should build paths via posPaymentPaths.ts (narrow scope)
rg "pos/payment" frontend/services/api --glob "*.ts"
```

- Expect **no** `initiate`/`confirm` matches.
- Under `frontend/services/api`, matches should be limited to `posPaymentPaths.ts`, `paymentService.ts`, `normalizePosPaymentMethods.ts`, and `index.ts` exports — not new ad-hoc route builders elsewhere.

**Admin (intentional, do not “fix” toward POS):** list/stats/cancel/refund for payments stay in `frontend-admin/src/api/legacy/payment.ts` → `/api/Payment/*`.
