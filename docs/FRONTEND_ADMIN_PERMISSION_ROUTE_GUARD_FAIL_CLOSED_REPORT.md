# Frontend-Admin PermissionRouteGuard – Fail-Closed Report

## 1. Fail-open nasıl kapatıldı

- **Eski risk:** `!permissions || permissions.length === 0 => allow` ile token’da permission yokken erişim veriliyordu.
- **Yapılanlar:**
  - **Varsayılan fail-closed:** Token’da `permissions` yok veya boş array ise artık **izin verilmiyor**; kullanıcı `/403`’e yönlendiriliyor.
  - **Açık state ayrımı:** Guard tek bir “allowed” hesabı yerine net durumlar kullanıyor:
    - `loading` → spinner
    - `unauthenticated` → null (AuthGate login’e yönlendirir)
    - `no_permissions` → redirect `/403`
    - `insufficient` → redirect `/403`
    - `allowed` → children render
  - **Migration flag (isteğe bağlı):** `NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS === 'true'` ise, permission olmasa da geçişe izin verilebiliyor. Varsayılan **kapalı**; production ve demo için davranış fail-closed.
  - **Config:** `src/shared/auth/routeGuardConfig.ts` içinde tek bir sabit; env ile override edilebiliyor.

---

## 2. Hangi ekranlar etkilendi

- **Tüm `(protected)` rotalar** layout’ta `PermissionRouteGuard` ile sarılı olduğu için etkileniyor:
  - `/dashboard`, `/products`, `/categories`, `/modifier-groups`, `/invoices`, `/orders`, `/payments`, `/audit-logs`, `/users`, `/settings`, `/receipt-templates`, `/receipt-generate`, `/customers`, `/receipts`, `/rksv/*`
- **Dashboard:** Artık “authenticated only” değil; **settings.view** gerekli (menü ile uyumlu).
- **Dinamik segmentler:** `/receipt-templates/[id]`, `/receipt-templates/new`, `/receipts/[receiptId]` için permission, parent path’e göre (longest prefix) çözülüyor; böylece bu ekranlar da aynı kurala tabi.

---

## 3. Hangi route’lara explicit permission eklendi / güncellendi

| Route | Permission | Not |
|-------|------------|-----|
| `/dashboard` | `settings.view` | Önceden boş; fail-closed ile deny önlemek için tanımlandı. |
| `/rksv` | `settings.view` | RKSV index/redirect için. |
| Diğerleri | (mevcut) | `routePermissions.ts` içinde zaten vardı. |
| **Path çözümü** | `getRequiredPermissionForPath(pathname)` | Exact match yoksa longest prefix (örn. `/receipt-templates/123` → `receipttemplate.view`). |

Tüm korumalı path’ler için tek kaynak: `ROUTE_PERMISSIONS` + `getRequiredPermissionForPath`. Boş/undefined path’ler artık yok; bilinmeyen path’ler için guard **red** dönüyor.

---

## 4. AdminOnlyGate ve role constants

- **AdminOnlyGate:** Artık **permission-first**: `user.manage` veya `settings.manage` varsa admin kabul; yoksa fallback **yalnızca** `role === 'Admin' || role === 'SuperAdmin'`. **Administrator** kullanılmıyor; sadece canonical Admin/SuperAdmin.
- **roles.ts:** Zaten sadece canonical roller (Admin, SuperAdmin, Manager, …); legacy/non-canonical sabit kaldırılacak bir şey yoktu.

---

## 5. Hangi FE testleri gerekli

- **PermissionRouteGuard:**
  - Authenticated + boş `permissions` → `/403`’e redirect (fail-closed).
  - Authenticated + gerekli permission var → children render.
  - Authenticated + gerekli permission yok → `/403`’e redirect.
  - Loading → spinner.
  - Unauthenticated → null (redirect AuthGate’te).
  - `NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS=true` iken boş permissions → allow (migration senaryosu).
- **getRequiredPermissionForPath:**
  - Exact match: `/settings` → `settings.view`.
  - Prefix match: `/receipt-templates/abc` → `receipttemplate.view`, `/rksv/status` → `settings.view`.
- **AdminOnlyGate:**
  - `user.manage` veya `settings.manage` ile → allow.
  - Sadece `Admin` / `SuperAdmin` role, permissions yok → allow (fallback).
  - Başka rol / yetkisiz → `/403`.

Değişen dosyalar: `PermissionRouteGuard.tsx`, `routePermissions.ts`, `routeGuardConfig.ts`, `AdminOnlyGate.tsx`, `menuPermissions.ts`.
