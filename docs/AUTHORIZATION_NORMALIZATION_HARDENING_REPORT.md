# Authorization Normalization Hardening — Report

**Current model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). **Administrator** is not a role (removed); use **Admin** only.

**Focus:** PaymentSecurityMiddleware and AuthController — canonical roles and permission-first enforcement.

---

## 1) Admin lockout risk (closed)

- **Token:** Identity role (e.g. "Admin") is canonical; JWT gets `role` and `permission` claims from `TokenClaimsService` and `RolePermissionMatrix`.
- **PaymentSecurityMiddleware:** **Permission-first, path-based.** No role allow-list. Path determines required permission (e.g. refund → `refund.create`). Admin receives `refund.create` (and other payment permissions) from matrix → allowed on refund path. **Admin is not blocked.**
- **Legacy note:** An earlier design used `AllowedPaymentRoles`; that was replaced by path → permission mapping and JWT permission claims.

---

## 2) Current implementation

### PaymentSecurityMiddleware.cs

| Aspect | Current state |
|--------|----------------|
| **Model** | Permission-first; path → required permission(s). |
| **Validation** | Reads JWT permission claims; user must have required permission for the request path. |
| **Admin** | Admin has payment permissions from RolePermissionMatrix → passes. |

### AuthController.cs

- Uses `Roles.*` constants (e.g. default role `Roles.Cashier`). Login/me response includes `role` (canonical) and `permissions` from matrix.

---

## 3) Smoke checklist

- [ ] Login as Admin → token has `role: "Admin"` and permission claims.
- [ ] Same token → POST /api/payment/refund (with valid body) → not 403 (payment security).
- [ ] Waiter token → POST /api/payment/refund → 403 (no refund.create).
