# Beta Smoke Checklist

Manual beta smoke checklist for the POS system. Payment, Receipt, RKSV, and FinanzOnline are high-risk flows; record evidence and stop on unexpected behavior.

## 1. Setup / Boot

- [ ] PostgreSQL is running and reachable.
- [ ] Backend starts with `ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, `JwtSettings__Issuer`, and `JwtSettings__Audience`.
- [ ] Non-development backend has `Cors__AllowedOrigins` set to the deployed admin/POS origins.
- [ ] Backend host/build environment uses the required .NET 10 SDK/runtime.
- [ ] No startup errors appear in backend logs.

## 2. Admin Build

- [ ] `NEXT_PUBLIC_API_BASE_URL` is set before build.
- [ ] `NEXT_PUBLIC_RKSV_ENVIRONMENT` is set to `TEST` or `PROD` before build.
- [ ] Admin build completes successfully.
- [ ] Built admin app points to the expected backend URL.

## 3. POS Startup

- [ ] `EXPO_PUBLIC_API_BASE_URL` is set before starting Expo.
- [ ] POS does not rely on a hardcoded LAN fallback.
- [ ] POS starts on the target device/emulator without config errors.
- [ ] Startup logs show the expected API base URL.

## 4. POS Login

- [ ] POS login screen loads.
- [ ] Cashier/test user can log in.
- [ ] Invalid credentials show a controlled user-facing error.
- [ ] Session remains active after basic navigation.

## 5. Admin Login

- [ ] Admin login page loads.
- [ ] Admin/test user can log in.
- [ ] Role-specific admin pages are accessible as expected.
- [ ] Unauthorized access is blocked with a controlled error.

## 6. POS Online Payment

- [ ] High-risk flow: Payment.
- [ ] POS can load products and create a cart while online.
- [ ] Test payment completes against the intended beta/test backend only.
- [ ] No sensitive card/payment data appears unmasked in logs.
- [ ] Payment failure path shows a controlled error and leaves the cart state understandable.

## 7. Receipt Fetch And Print

- [ ] High-risk flow: Receipt.
- [ ] Receipt can be fetched after a successful test payment.
- [ ] Receipt includes expected beta/test identifiers.
- [ ] Print action reaches the configured test printer or print preview.
- [ ] Print failure shows a controlled error without duplicating the receipt.

## 8. Offline Pending And Replay

- [ ] POS can detect offline state.
- [ ] Offline action is marked pending and visible to the tester.
- [ ] Returning online triggers replay/sync.
- [ ] Replay does not duplicate the original operation.
- [ ] Replay result is visible and auditable.

## 9. RKSV / FinanzOnline Readiness

- [ ] High-risk flows: RKSV and FinanzOnline.
- [ ] `NEXT_PUBLIC_RKSV_ENVIRONMENT` matches the intended beta mode.
- [ ] TSE/RKSV readiness is visible before fiscal test actions.
- [ ] FinanzOnline test connectivity/readiness is confirmed before receipt testing.
- [ ] Any unavailable fiscal dependency blocks the high-risk test and is logged clearly.

## 10. Go / No-Go Decision

- [ ] No blocker remains in setup, login, payment, receipt, offline replay, RKSV, or FinanzOnline checks.
- [ ] All high-risk flow evidence is captured.
- [ ] Known non-blocking issues are documented with owner and follow-up date.
- [ ] Decision recorded: Go / No-Go.
- [ ] Approver name and timestamp recorded.
