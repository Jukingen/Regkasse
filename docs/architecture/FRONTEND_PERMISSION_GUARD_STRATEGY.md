# Frontend Permission-Based Guard Stratejisi

**Tarih:** 2025-03-09  
**Hedef:** Role değil permission merkezli UI kararları; route guard, menü görünürlüğü, aksiyon butonları. Minimum kırılımlı geçiş (Admin/Administrator tek kaynak: permission set).

---

## Mimari özet

- **Tek kaynak:** Backend `/api/Auth/me` (ve login) dönen `permissions: string[]` (resource.action). Tüm route/menü/buton kararları bu listeye göre.
- **Katmanlar:** (1) **Route guard** → sayfaya giriş; (2) **Page-level guard** → sayfa içi bölüm/sekme gizleme; (3) **Component/button-level guard** → buton disable veya gizleme.
- **Bileşenler:** `PermissionRouteGuard` (layout), `PermissionGate` / `PermissionGateButton` (sayfa/buton), `usePermissions()` (hasPermission, hasAnyPermission, hasAllPermissions), `ROUTE_PERMISSIONS` / `MENU_PERMISSION` + `isMenuItemAllowed`.
- **Fallback:** Backend henüz `permissions` göndermiyorsa boş dizi; menü/guard isteğe bağlı olarak `role` ile fallback yapabilir (migration dönemi). Sonrasında sadece permission.

---

## 1) Auth user model önerisi

Backend `/api/Auth/me` (ve login response) artık `role`, `roles` ve `permissions` döndürüyor. Frontend’te tek tip: hem mevcut `UserInfo` ile uyumlu hem de permission listesi taşıyan bir model kullanın.

```typescript
// Önerilen: shared/auth/types.ts veya features/auth/types.ts

/** Backend /me ve login user object – role + permissions. */
export interface AuthUser {
  id: string | null;
  userName?: string | null;
  email?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  /** Canonical role (Administrator → Admin). */
  role?: string | null;
  /** All role names from Identity (legacy Administrator may appear). */
  roles?: string[];
  /** Permission strings from backend (resource.action). Single source for UI guards. */
  permissions: string[];
  employeeNumber?: string | null;
  taxNumber?: string | null;
  notes?: string | null;
  isActive?: boolean;
  createdAt?: string | null;
  lastLoginAt?: string | null;
}
```

**Migration:** Mevcut `UserInfo` kullanımını koruyun; `fetchUser` içinde API cevabına `permissions: res.permissions ?? []` ve `roles: res.roles ?? []` ekleyip `AuthUser` olarak döndürün. Backend henüz `permissions` göndermiyorsa boş dizi kullanın; menü/guard’lar önce `permissions`’a bakıp yoksa `role` fallback’i yapabilir (geçiş dönemi).

---

## 2) hasPermission / hasAnyPermission / hasAllPermissions

Permission listesi kullanıcı objesinde; helper’lar bu listeyi kullanır. Rol fallback isteğe bağlı (eski API’lerde permissions boşken).

```typescript
// shared/auth/permissions.ts

/** Permission strings – backend AppPermissions ile senkron tutulmalı. */
export const PERMISSIONS = {
  USER_VIEW: 'user.view',
  USER_MANAGE: 'user.manage',
  PRODUCT_VIEW: 'product.view',
  PRODUCT_MANAGE: 'product.manage',
  CATEGORY_VIEW: 'category.view',
  CATEGORY_MANAGE: 'category.manage',
  ORDER_VIEW: 'order.view',
  ORDER_CREATE: 'order.create',
  ORDER_UPDATE: 'order.update',
  PAYMENT_VIEW: 'payment.view',
  PAYMENT_TAKE: 'payment.take',
  PAYMENT_CANCEL: 'payment.cancel',
  REFUND_CREATE: 'refund.create',
  SETTINGS_VIEW: 'settings.view',
  SETTINGS_MANAGE: 'settings.manage',
  AUDIT_VIEW: 'audit.view',
  AUDIT_EXPORT: 'audit.export',
  AUDIT_CLEANUP: 'audit.cleanup',
  REPORT_VIEW: 'report.view',
  REPORT_EXPORT: 'report.export',
  INVOICE_VIEW: 'invoice.view',
  INVOICE_MANAGE: 'invoice.manage',
  INVOICE_EXPORT: 'invoice.export',
  CREDIT_NOTE_CREATE: 'creditnote.create',
  FINANZONLINE_MANAGE: 'finanzonline.manage',
  FINANZONLINE_SUBMIT: 'finanzonline.submit',
  RECEIPT_TEMPLATE_VIEW: 'receipttemplate.view',
  RECEIPT_TEMPLATE_MANAGE: 'receipttemplate.manage',
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

/** Tek permission kontrolü. */
export function hasPermission(
  user: { permissions?: string[] } | null | undefined,
  permission: string
): boolean {
  if (!user?.permissions?.length) return false;
  return user.permissions.includes(permission);
}

/** Herhangi biri varsa true. */
export function hasAnyPermission(
  user: { permissions?: string[] } | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.some((p) => user!.permissions!.includes(p));
}

/** Hepsi varsa true. */
export function hasAllPermissions(
  user: { permissions?: string[] } | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.every((p) => user!.permissions!.includes(p));
}
```

Hook ile kullanım:

```typescript
// shared/auth/usePermissions.ts

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useMemo } from 'react';
import { hasPermission, hasAnyPermission, hasAllPermissions } from './permissions';

export function usePermissions() {
  const { user } = useAuth();
  return useMemo(
    () => ({
      user,
      hasPermission: (permission: string) => hasPermission(user, permission),
      hasAnyPermission: (permissions: string[]) => hasAnyPermission(user, permissions),
      hasAllPermissions: (permissions: string[]) => hasAllPermissions(user, permissions),
    }),
    [user]
  );
}
```

---

## 3) Route config örneği

Next.js App Router’da merkezi route listesi tutup, her path için gerekli permission’ı tanımlayın; guard bileşeni bu listeyi kullanır.

```typescript
// shared/auth/routePermissions.ts

import { PERMISSIONS } from './permissions';

/** Path → en az bir bu permission gerekir (route’a giriş). */
export const ROUTE_PERMISSIONS: Record<string, string | string[]> = {
  '/dashboard': [], // authenticated only
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/orders': PERMISSIONS.ORDER_VIEW,
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/users': PERMISSIONS.USER_VIEW,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
};
```

```tsx
// shared/auth/PermissionRouteGuard.tsx

'use client';

import { usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, hasAnyPermission } from './permissions';
import { ROUTE_PERMISSIONS } from './routePermissions';
import { ReactNode, useEffect } from 'react';
import { Spin } from 'antd';

interface PermissionRouteGuardProps {
  children: ReactNode;
}

function checkRoutePermission(
  pathname: string,
  permissions: string[] | undefined
): boolean {
  const required = ROUTE_PERMISSIONS[pathname];
  if (required === undefined || (Array.isArray(required) && required.length === 0))
    return true;
  if (!permissions?.length) return false;
  const arr = Array.isArray(required) ? required : [required];
  return arr.some((p) => permissions.includes(p));
}

export function PermissionRouteGuard({ children }: PermissionRouteGuardProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, authStatus, isInitialized } = useAuth();

  const allowed = checkRoutePermission(pathname, user?.permissions);

  useEffect(() => {
    if (!isInitialized || authStatus !== 'authenticated') return;
    if (!allowed) router.replace('/403');
  }, [isInitialized, authStatus, allowed, router]);

  if (!isInitialized || authStatus === 'loading') {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" tip="Checking access..." />
      </div>
    );
  }

  if (!allowed) return null;
  return <>{children}</>;
}
```

Kullanım: `(protected)/layout.tsx` içinde `AuthGate` sonrası `PermissionRouteGuard` ile sarmalayın; böylece sadece authenticated + route permission’ı olan kullanıcı sayfayı görür.

---

## 4) Menu config örneği

Menü öğelerini permission ile filtreleyin; `role` yerine `permissions` kullanın.

```typescript
// shared/auth/menuConfig.tsx

import type { MenuProps } from 'antd';
import Link from 'next/link';
import {
  DashboardOutlined,
  FileTextOutlined,
  UserOutlined,
  SettingOutlined,
  SafetyCertificateOutlined,
  CreditCardOutlined,
  SafetyOutlined,
} from '@ant-design/icons';
import { PERMISSIONS } from './permissions';

export interface MenuItemConfig {
  key: string;
  icon: React.ReactNode;
  label: React.ReactNode;
  /** En az bir permission gerekir (boş = sadece giriş). */
  permission?: string | string[];
  children?: MenuItemConfig[];
}

export const MENU_ITEMS: MenuItemConfig[] = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: <Link href="/dashboard">Dashboard</Link> },
  { key: '/invoices', icon: <FileTextOutlined />, label: <Link href="/invoices">Rechnungen</Link>, permission: PERMISSIONS.INVOICE_VIEW },
  { key: '/products', icon: <FileTextOutlined />, label: <Link href="/products">Produkte</Link>, permission: PERMISSIONS.PRODUCT_VIEW },
  { key: '/modifier-groups', icon: <FileTextOutlined />, label: <Link href="/modifier-groups">Add-on-Gruppen</Link>, permission: PERMISSIONS.PRODUCT_VIEW },
  { key: '/categories', icon: <FileTextOutlined />, label: <Link href="/categories">Kategorien</Link>, permission: PERMISSIONS.CATEGORY_VIEW },
  { key: '/customers', icon: <UserOutlined />, label: <Link href="/customers">Kunden</Link> },
  { key: '/receipts', icon: <FileTextOutlined />, label: <Link href="/receipts">Belege</Link> },
  { key: '/receipt-templates', icon: <FileTextOutlined />, label: <Link href="/receipt-templates">Belegvorlagen</Link>, permission: PERMISSIONS.RECEIPT_TEMPLATE_VIEW },
  { key: '/audit-logs', icon: <SafetyCertificateOutlined />, label: <Link href="/audit-logs">Audit-Logs</Link>, permission: PERMISSIONS.AUDIT_VIEW },
  { key: '/payments', icon: <CreditCardOutlined />, label: <Link href="/payments">Zahlungen</Link>, permission: PERMISSIONS.PAYMENT_VIEW },
  { key: '/users', icon: <UserOutlined />, label: <Link href="/users">Benutzer</Link>, permission: PERMISSIONS.USER_VIEW },
  { key: '/settings', icon: <SettingOutlined />, label: <Link href="/settings">Einstellungen</Link>, permission: PERMISSIONS.SETTINGS_VIEW },
  {
    key: '/rksv',
    icon: <SafetyOutlined />,
    label: 'RKSV',
    permission: PERMISSIONS.FINANZONLINE_MANAGE,
    children: [
      { key: '/rksv/status', label: <Link href="/rksv/status">Status</Link> },
      { key: '/rksv/finanz-online-queue', label: <Link href="/rksv/finanz-online-queue">FinanzOnline Queue</Link>, permission: PERMISSIONS.FINANZONLINE_MANAGE },
    ],
  },
];

function itemAllowed(item: MenuItemConfig, permissions: string[] | undefined): boolean {
  if (!item.permission) return true;
  const arr = Array.isArray(item.permission) ? item.permission : [item.permission];
  return arr.some((p) => permissions?.includes(p));
}

export function filterMenuByPermission(
  items: MenuItemConfig[],
  permissions: string[] | undefined
): MenuProps['items'] {
  return items
    .filter((item) => itemAllowed(item, permissions))
    .map((item) => {
      const children = item.children?.length
        ? filterMenuByPermission(item.children, permissions)
        : undefined;
      return {
        key: item.key,
        icon: item.icon,
        label: item.label,
        children: children?.length ? children : undefined,
      };
    });
}
```

Layout’ta: `filterMenuByPermission(MENU_ITEMS, user?.permissions)` ile `Menu` items’ı geçirin.

### Component/button-level guard (PermissionGate)

Sayfa içinde bölüm gizlemek veya butonu disable göstermek için:

```tsx
// shared/auth/PermissionGate.tsx – hide: render nothing; disable: render with wrapper + tooltip

import { PermissionGate, PermissionGateButton } from '@/shared/auth/PermissionGate';
import { PERMISSIONS } from '@/shared/auth/permissions';

// Bölümü tamamen gizle (örn. Audit Cleanup sadece audit.cleanup yetkisi olanlara)
<PermissionGate permission={PERMISSIONS.AUDIT_CLEANUP} mode="hide">
  <CleanupSection />
</PermissionGate>

// Butonu yetkisizde disabled görünüm + tooltip
<PermissionGate permission={PERMISSIONS.SETTINGS_MANAGE} mode="disable" fallbackTooltip="Sie haben keine Berechtigung …">
  <Button type="primary">Speichern</Button>
</PermissionGate>

// Veya PermissionGateButton (wrapper span + tooltip; buton tıklanamaz)
<PermissionGateButton permission={PERMISSIONS.INVOICE_EXPORT}>
  <Button>Export CSV</Button>
</PermissionGateButton>
```

Alternatif: Hook ile `disabled` doğrudan butona verilir (tercih edilebilir, Ant Design Button native disabled kullanır):

```tsx
const { hasPermission } = usePermissions();
<Tooltip title={!hasPermission(PERMISSIONS.INVOICE_EXPORT) ? 'Sie haben keine Berechtigung …' : undefined}>
  <span>
    <Button disabled={!hasPermission(PERMISSIONS.INVOICE_EXPORT)}>Export CSV</Button>
  </span>
</Tooltip>
```

---

## 5) Hide vs disable stratejisi

| Durum | Öneri | Gerekçe |
|--------|--------|--------|
| **Menü öğesi** | **Hide** | Yetkisi yoksa sayfaya girmemeli; menüde gösterilmemeli. |
| **Sayfa içi sekme / bölüm** | **Hide** | Örn. “FinanzOnline” sekmesi sadece ilgili permission’a sahipse görünsün. |
| **Aksiyon butonu (create, delete, export)** | **Disable** + tooltip | Kullanıcı sayfada; “Bu Aktion ist für Ihre Rolle nicht freigegeben” gibi bilgi verilir; yetkisiz tıklama engellenir. |
| **Form alanı (read-only sayfa)** | **Hide** | Sadece görüntüleme yetkisi yoksa alan grubu gizlenir. |
| **Kritik aksiyon (refund, audit cleanup)** | **Disable** + tooltip | Yanlışlıkla tıklamayı engellemek ve nedenini göstermek için. |
| **Tablo sütunu (hassas veri)** | **Hide** | Örn. sadece AuditAdmin’in gördüğü sütun; permission yoksa sütun render edilmez. |

**Kural:** Sayfa/route erişimi ve menü = **hide**. Sayfa içindeki aksiyonlar = **disable + tooltip** (kullanıcı bağlamı korunur, eğitim/feedback kolaylaşır). İstisna: Tamamen farklı bir modül sekmesi (örn. RKSV) = hide.

---

## 6) Ekran bazlı örnekler

### Admin Products

- **Route:** `permission: product.view`.
- **Sayfa:** Liste görünür. “Neues Produkt”, “Bearbeiten”, “Löschen”, “Lager anpassen” butonları:
  - `product.manage` yoksa **disabled** + tooltip “Sie haben keine Berechtigung …”.
- **Menü:** `product.view` varsa “Produkte” görünsün.

### Company Settings

- **Route:** `permission: settings.view` (sadece okuma da yetebilir).
- **Sayfa:** Form alanları:
  - `settings.manage` yoksa tüm form **read-only** veya “Speichern” **disabled** + tooltip.
  - “Export”, “Banking”, “FinanzOnline” gibi alt bölümler: ilgili permission yoksa bölüm **hide** veya disabled.
- **Menü:** `settings.view` varsa “Einstellungen” görünsün.

### POS Checkout (Expo / POS client)

- **Route / flow:** `sale.view` + `sale.create` (sepet); `payment.take` (ödeme alındı).
- **Butonlar:** “Zahlung abschließen” → `payment.take`; “Stornieren” → `refund.create`; “Zahlung stornieren” → `payment.cancel`. Yetki yoksa **disable** + kurumsal mesaj.

### Orders (Liste / Status-Update)

- **Route:** `order.view`.
- **Liste:** Görünür. “Status ändern”, “Stornieren” → `order.update`; yoksa **disabled** + tooltip.
- **Menü:** `order.view` varsa “Aufträge” görünsün.

### Audit Logs

- **Route:** `audit.view`.
- **Sayfa:** Liste ve filtreler. “Export” → `audit.export` (yoksa **disabled**). “Alte Einträge löschen” / Cleanup → `audit.cleanup` (yoksa **hide** veya disabled).
- **Menü:** `audit.view` varsa “Audit-Logs” görünsün.

### Invoice Export

- **Route:** `invoice.view`.
- **Aksiyon:** “Export” / “CSV” butonu → `invoice.export`; yoksa **disabled** + tooltip.
- Menü zaten `invoice.view` ile gösterilmiş olur.

### FinanzOnline

- **Route / RKSV menü:** `finanzonline.manage` (Queue, Config).
- **Sayfa:** Config düzenleme → `finanzonline.manage`; “Faktur senden” / Submit → `finanzonline.submit`. Submit yetkisi yoksa buton **disabled** + tooltip.
- **Menü:** RKSV alt menüsü `finanzonline.manage` (veya `settings.view`) ile filtrelenir.

### Categories (Kategorien)

- **Route:** `category.view` (ROUTE_PERMISSIONS['/categories']).
- **Sayfa:** Liste görünür. “Neue Kategorie”, “Bearbeiten”, “Löschen” → `category.manage` yoksa **disabled** + tooltip.
- **Menü:** `category.view` varsa “Kategorien” görünsün (MENU_PERMISSION['/categories']).

### Inventory Adjust (Lager anpassen)

- **Route:** Ürün detayı veya ayrı Envanter sayfası varsa `inventory.view` ile korunur; “Lager anpassen” aksiyonu → `inventory.adjust`.
- **Sayfa:** Liste/grid görünür; “Bestand anpassen” / “Lager anpassen” butonu → `inventory.adjust` yoksa **disabled** + tooltip “Sie haben keine Berechtigung …”.
- **Menü:** Envanter menü öğesi `inventory.view` ile filtrelenir.

---

## 7) Kısa UI stratejisi notları

- **Tek kaynak:** Backend’den gelen `permissions` array’i. Role sadece fallback veya gösterim (örn. “Eingeloggt als Admin”).
- **Admin / Administrator:** Backend token’da `role: "Admin"` ve aynı permission set’i veriyor; frontend sadece `permissions`’a baksın, rol adı çakışması UI’da yok.
- **Legacy:** API’de `permissions` yoksa `permissions = []` kabul edin; menü/guard’da “permissions boşsa role ile fallback” yapılabilir (geçiş süresi); sonra fallback kaldırılır.
- **403 sayfası:** PermissionRouteGuard yetkisiz route’ta `/403`’e yönlendirir; 403 sayfasında “Keine Berechtigung für diese Seite” mesajı gösterin.
- **POS + Backoffice:** Aynı permission sabitleri (backend ile senkron); POS tarafında da `hasPermission(user, 'payment.take')` gibi kullanım. Menü/config uygulama bazlı (admin = sidebar, POS = tab/drawer).
- **Test:** Mock user ile `permissions: ['product.view']` verip menüde sadece Produkte göründüğünü; `product.manage` eklenince butonun enable olduğunu doğrulayın.

---

## 8) Proje dosyaları (TypeScript)

| Amaç | Dosya |
|------|--------|
| AuthUser tipi | `frontend-admin/src/shared/auth/types.ts` |
| Permission sabitleri + hasPermission / hasAnyPermission / hasAllPermissions | `frontend-admin/src/shared/auth/permissions.ts` |
| usePermissions hook | `frontend-admin/src/shared/auth/usePermissions.ts` |
| Route → permission map | `frontend-admin/src/shared/auth/routePermissions.ts` |
| Menü key → permission + isMenuItemAllowed | `frontend-admin/src/shared/auth/menuPermissions.ts` |
| Route guard (redirect /403) | `frontend-admin/src/shared/auth/PermissionRouteGuard.tsx` |
| Sayfa/buton guard (hide veya disable+tooltip) | `frontend-admin/src/shared/auth/PermissionGate.tsx` (PermissionGate, PermissionGateButton) |

**Kullanım örnekleri:**

- **Route guard:** `(protected)/layout.tsx` içinde `AuthGate` sonrası `<PermissionRouteGuard>{children}</PermissionRouteGuard>`.
- **Menü:** `filteredItems = menuItems.filter(item => isMenuItemAllowed(item.key, user?.permissions))` veya layout’ta `filterMenuByPermission(MENU_ITEMS, user?.permissions)` (MENU_ITEMS ayrı config’te tanımlıysa).
- **Buton disable:** `<PermissionGate permission={PERMISSIONS.SETTINGS_MANAGE} mode="disable">{<Button>Speichern</Button>}</PermissionGate>` veya `disabled={!hasPermission(PERMISSIONS.SETTINGS_MANAGE)}` + Tooltip.
- **Bölüm gizleme:** `<PermissionGate permission={PERMISSIONS.AUDIT_CLEANUP}>{<CleanupSection />}</PermissionGate>`.

---

## 9) Uygulama notları

1. **Backend /me:** `/api/Auth/me` cevabında `permissions: string[]` ve isteğe bağlı `roles: string[]` dönmeli. `fetchUser` (useAuth) bu alanları `AuthUser`/UserInfo’ya map etmeli; generated `UserInfo` tipinde yoksa genişletme veya adapter kullanın.
2. **Layout menü:** Mevcut layout `canViewUsers(user?.role)` / `canShowRksvMenu(user?.role)` kullanıyorsa, migration’da önce `user?.permissions` varsa `isMenuItemAllowed(key, user.permissions)` ile filtreleyin; yoksa role fallback. Hedefte sadece permission.
3. **PermissionRouteGuard:** `(protected)/layout.tsx`’e eklendiğinde tüm korumalı sayfalar için tek noktadan kontrol. ROUTE_PERMISSIONS’da olmayan path’ler “authenticated only” kabul edilir (boş dizi).
4. **403 sayfası:** `/403` route’u mevcut; PermissionRouteGuard yetkisiz kullanıcıyı oraya yönlendirir. Sayfada Almanca mesaj: “Keine Berechtigung für diese Seite.”
5. **Yeni permission:** Backend `AppPermissions` ve `PermissionCatalog`’a eklenen her permission’ı `frontend-admin/src/shared/auth/permissions.ts` içindeki `PERMISSIONS` objesine ekleyin; route/menü config’te ihtiyaç varsa kullanın.
6. **POS (Expo):** Aynı permission string’leri (resource.action) kullanılabilir; backend JWT’de permission claim’leri POS client’a da gider. POS tarafında benzer guard/helper yapısı kurulabilir.
