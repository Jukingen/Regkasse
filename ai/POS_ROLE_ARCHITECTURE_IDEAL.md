# Decision Memo: Ideal POS Role Architecture (Capability-Based)

**Status:** Architecture note (implementation-agnostic)  
**Audience:** Product, backend, compliance  
**Goal:** Reduce role sprawl; permission/capability-first; maintainable governance.

---

## 1. Why “too many roles” is harmful

| Problem | Effect |
|--------|--------|
| **Combinatorial explosion** | Each new role × matrix row × UI gate × audit rule = N² maintenance. |
| **Duplicate semantics** | “Waiter” vs “Kellner”, “BranchManager” vs “Manager” → same behavior, different names → migration debt. |
| **Authorization drift** | Code checks role name strings; matrix and JWT get out of sync; custom roles bypass matrix. |
| **Onboarding cost** | Operators must understand 10+ roles; wrong assignment is common. |
| **Compliance ambiguity** | Audit asks “who could do X?” — many roles make the answer a long list instead of a capability query. |
| **Tenant variance** | Every customer wants slightly different mixes; encoding that as new roles does not scale. |

**Principle:** Roles should be **few and stable**; variation should live in **permissions, presets, or attributes** (not new role rows).

---

## 2. Ideal minimal system role set

**System roles** = immutable, seeded, non-deletable; map to **employment / trust tier**, not to every job title.

| Role | Purpose |
|------|--------|
| **SuperAdmin** | Tenant setup, billing, user lifecycle, break-glass; smallest population. |
| **Operator** | Day-to-day POS: sales, payments, cart, tables, kitchen handoff; default for floor staff. |
| **Backoffice** | Catalog, users (non-SuperAdmin), reports, exports, configuration that is not fiscal-critical. |
| **Kitchen** | Station-only: order queue, status updates; minimal surface. |

Optional fifth if legally/operationally required:

| Role | Purpose |
|------|--------|
| **ReadOnly** (or **Viewer**) | Audit/report read-only; no write anywhere. |

**Target count:** **4–5 system roles** plus **custom roles** only where a tenant truly needs a unique permission mix (and even then, prefer preset + overrides).

---

## 3. Persona vs permission — clear separation

| Concept | Definition | Where it lives |
|--------|------------|----------------|
| **Persona** | UX label / training concept (“cashier”, “waiter”) — **not** an authorization primitive. | Docs, UI copy, onboarding. |
| **Permission (capability)** | Atomic action on a resource: `payment.take`, `report.export`, `user.manage`. | Catalog + matrix or role claims. |
| **Preset** | Named bundle of permissions (e.g. “Floor staff”, “Shift lead”) — **assignable** without new Identity role. | DB or config; applied to a user or to a custom role. |
| **System role** | Stable trust boundary + default permission set; **immutable** at runtime. | Seed + matrix only. |
| **Attribute / flag** | Cross-cutting state: `IsDemo`, `branchId`, `canOpenDrawer`. | User or session; **never** a role. |

**Rule:** If it’s “sometimes on, sometimes off” per user without changing employment tier → **not** a new role; use permission or attribute.

---

## 4. Evaluation: SuperAdmin / Operator / Backoffice / Kitchen

| Role | Fits POS? | Notes |
|------|-----------|--------|
| **SuperAdmin** | Yes | Single top tier; avoids Admin/Administrator/Demo splits. |
| **Operator** | Yes | Replaces Cashier + Waiter + many floor variants; table service vs counter is **workflow**, not auth tier. |
| **Backoffice** | Yes | Manager/Accountant/ReportViewer converge here unless law mandates separation (then add ReadOnly). |
| **Kitchen** | Yes | Keeps blast radius small; kiosk/tablet at kitchen should not get payment.take. |

**Trade-off:** Coarser roles mean **more permission granularity** inside Operator/Backoffice (e.g. `table.manage` vs `payment.refund`) — which is desirable: same role, different presets.

---

## 5. Cashier / Waiter / BranchManager / ReportViewer / Auditor — role vs preset vs permission set

| Concept | Recommendation | Rationale |
|--------|----------------|-----------|
| **Cashier** | **Preset** (or workflow profile) under **Operator** | Same trust tier; difference is which screens are primary — preset selects defaults, not identity. |
| **Waiter** | **Preset** under **Operator** | Same as cashier; “takes payments at table” = permission subset, not new role. |
| **BranchManager** | **Backoffice** + **scope attribute** (`branchId` / `managedBranches`) | “Manager” is scope, not a separate security primitive unless branches are separate tenants. |
| **ReportViewer** | **Permission set** on Backoffice or **ReadOnly** role | Read-only is one capability dimension; if mixed with write elsewhere, use custom role with claims. |
| **Auditor** | **Permission set** (`audit.view`, `audit.export`) + optional **ReadOnly** | Rarely needs its own role if Backoffice can be restricted to audit-only via preset. |

**Summary:** Job titles → **presets**; org scope → **attributes**; read vs write → **permissions**; only trust tier → **system role**.

---

## 6. Why Demo must not be a role

| Reason | Explanation |
|--------|-------------|
| **Not employment tier** | Demo is **mode** (training/sandbox), not a job. |
| **Same person, two modes** | A real cashier testing flow should not need a second account or role switch. |
| **Authorization pollution** | Role-based checks become `if (role == Demo) … else …` everywhere; matrix grows fake rows. |
| **Fiscal ambiguity** | RKSV needs clear “fiscal vs non-fiscal” — that is **transaction/session/device** state (`IsDemoFiscal`, TSE mode), not Identity role. |
| **Cleanup cost** | Migrating off Demo role requires user reassignment; flag migration is trivial. |

**Model:** `IsDemo` (user or session) + **server-side** block on fiscal writes; receipt labeling via **payment/TSE flags**, not role name.

---

## 7. Transition strategy: short-term applicable → long-term ideal

### Short-term (applicable now without big bang)

- Keep **current canonical roles** but treat **Waiter/Cashier/Manager** as **presets in documentation** and optionally in UI (preset selector for custom roles only).
- **Freeze** new system roles; new needs → custom role + claims or preset.
- **Demo:** already moving to **IsDemo**; complete migration and remove Demo from AspNetRoles everywhere.
- **Single source of truth** for “who can pay/refund” = permission checks; role name only for display.

### Medium-term

- Introduce **preset** concept in admin (named permission bundles) without reducing system roles yet.
- Map **ReportViewer / Accountant** to same Backoffice tier with different presets; deprecate redundant role names via migration when user counts allow.

### Long-term (ideal)

- Reduce system roles to **SuperAdmin, Operator, Backoffice, Kitchen** (+ optional ReadOnly).
- **Cashier/Waiter** become **Operator + preset**; **BranchManager** becomes **Backoffice + scope**.
- Matrix shrinks to 4–5 rows; all variation in **PermissionCatalog + presets + optional custom roles**.

### Migration pattern

1. **Add** new role (e.g. Operator) and seed it.  
2. **Migrate** users from Cashier/Waiter to Operator **without** removing old roles until clients updated.  
3. **Presets** replicate old behavior (same effective permissions).  
4. **Deprecate** old roles when zero users; delete rows in controlled migration.

---

## Decision summary

| Decision | Choice |
|----------|--------|
| Role count | Minimize; target 4–5 system roles |
| Job titles | Presets, not roles |
| Demo | User/session flag + fiscal flags; never Identity role |
| Variance | Permissions + presets + attributes |
| Long-term set | SuperAdmin, Operator, Backoffice, Kitchen (+ optional ReadOnly) |

---

## References (conceptual)

- **RBAC vs ABAC:** Prefer RBAC with **fat permission catalog** over many roles (ABAC-lite via attributes for scope).  
- **RKSV:** Fiscal truth in signed receipts; demo labeling via TSE/payment metadata, not role name.
