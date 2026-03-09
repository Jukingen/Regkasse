# Role Constants Cleanup — Report

**Historical.** The legacy admin role has been fully removed. The system uses **Admin** only (plus SuperAdmin). This document is kept for reference.

---

## 1) Canonical roles (current)

- **Core:** SuperAdmin, Admin, Manager, Cashier, Waiter.
- **Optional:** Kitchen, ReportViewer, Accountant.
- **List:** `Roles.Canonical` — SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. Single admin role is Admin.

---

## 2) Other roles (seed literals)

- Kellner, Auditor, Demo, BranchManager — no constant in `Roles`; seeded as string literals where needed.

---

## 3) Seed

- RoleSeedData seeds only `Roles.*` and the above literals; no legacy admin role. Single admin role is Admin.

---

## 4) Script / migration

- `CanonicalizeLegacyRoleNames.sql`: Deprecated / no-op; do not run.
- Migration of the same name: historical record only.
