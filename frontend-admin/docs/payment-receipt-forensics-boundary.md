# Admin Payment vs Receipt Forensics Boundary

## Intentional split

- Admin payment operations stay in the **legacy containment lane** (`/api/Payment` family and existing admin payment wrappers/hook usage).
- POS/mobile payment execution stays in the **canonical POS lane** (`/api/pos/payment`).
- Receipt and signature-debug diagnostics are treated as **receipt forensics**, not payment operations.

## Admin forensic flow

`Payment detail` -> `receipt id` (direct or by-payment lookup) -> `receipt detail` -> `receipt signature-debug`

- Receipt list/detail/by-payment/signature-debug are centralized in:
  - `src/features/receipts/api/forensics-client.ts`
- UI hooks for receipts consume this boundary:
  - `useReceiptListQuery`
  - `useReceiptDetailQuery`
  - `useSignatureDebugQuery`

## Why this is safer

- Avoids mixing payment-operation endpoints with receipt-forensic endpoints.
- Keeps payment mutation scope (cancel/refund) separate from forensic read scope.
- Reduces page-level request duplication and clarifies ownership for future maintenance.
