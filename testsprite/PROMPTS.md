# Regkasse TestSprite Prompt Paketi (10 Adet)

Bu promptlar proje kodu analiz edilerek guncellenmistir ve Cursor chat'te dogrudan kopyala-yapistir icin hazirlanmistir.

## 1) Auth Smoke

```text
TestSprite: Regkasse backend icin authentication smoke testleri olustur ve calistir.
Kapsam:
- GET /api/health -> 200
- POST /api/Auth/login ile email login basarili
- POST /api/Auth/login ile username login basarili
- Gecersiz kimlik bilgisi -> 400 (bu projede 401 varsayma)
- GET /api/Auth/me (valid token ile) -> 200
Oncelik: kritik patika, kisa smoke seti.
```

## 2) Auth Guvenlik ve Session

```text
TestSprite: Regkasse auth akisi icin guvenlik odakli testler olustur.
Kapsam:
- Invalid credentials denemeleri
- Yetkisiz /api/Auth/me erisimi reddedilmeli
- POST /api/Auth/refresh yenileme akisi
- POST /api/Auth/logout ve /api/Auth/logout-all davranisi
- Token suresi doldugunda beklenen davranis
Rapor: basarisiz testlerde root-cause ozeti ver.
```

## 3) Multi-Tenant Izolasyon (P0)

```text
TestSprite: Regkasse multi-tenant isolation testleri olustur ve calistir.
Kurallar:
- Tenant A, Tenant B verisini gorememeli
- Cross-tenant resource access -> 404 (403 degil)
- Ozellikle /api/admin/payments ve /api/admin/users yuzeylerinde tenant context dogrulanmali
Oncelik: P0 security.
```

## 4) Super Admin Impersonation

```text
TestSprite: Super Admin impersonation akislarini test et.
Kapsam:
- POST /api/admin/tenants/{tenantId}/impersonate basarili olmali
- Impersonation token tenant_id claim icermeli
- Impersonated oturumda baska tenant verisine erisim olmamali
- Kritik endpointlerde (/api/admin/payments, /api/admin/users) tenant boundary korunmali
```

## 5) User Management API

```text
TestSprite: Regkasse user management API testlerini olustur.
Kapsam:
- POST /api/admin/users ile Super Admin yeni kullanici olusturur (auto-generated username)
- Username unique ve case-insensitive kontrol
- PATCH /api/admin/users/{id}/username update + reason ile audit izi
- POST /api/admin/users/{id}/generate-temporary-password ve force password change
```

## 6) POS Kritik Odeme Akisi

```text
TestSprite: POS kritik akis testleri olustur ve calistir.
Kapsam:
- Canonical route olarak POST /api/pos/payment kullan (legacy /api/Payment sadece alias)
- Odeme olusturma (cash) ve payment id donusu
- GET /api/pos/payment/{id} ile odeme kaydi dogrulama
- GET /api/pos/payment/{id}/receipt ile fis dogrulama
- GET /api/pos/payment/{id}/qr.png veya qr.svg endpointlerini dogrulama
Rapor: odeme -> fis tutarliligi icin assertion listesi ver.
```

## 7) Fiskal/RKSV Guvenlik Kurallari

```text
TestSprite: Regkasse fiskal kurallari icin negatif/pozitif testler olustur.
Kapsam:
- POST /api/pos/payment/storno akisinda OriginalReceiptNumber zorunlu
- StornoReason zorunlu oldugu yerde bos gecilememeli
- IsStorno ve IsRefund ayni anda true olamaz
- Uygun olmayan durumda voucher/offline replay reddedilmeli
- Kritik endpointlerde uygun hata kodu ve mesaji donmeli
Not: Compliance ihlali olursa ayri olarak "critical risk" diye isle.
```

## 8) Backup API Regression

```text
TestSprite: Backup API regression test seti olustur.
Kapsam:
- GET /api/admin/backup/settings
- PUT /api/admin/backup/settings
- GET /api/admin/backup/execution-mode
- POST /api/admin/backup/trigger
- GET /api/admin/backup/status/latest
- GET /api/admin/backup/runs
- GET /api/admin/backup/recoverability-summary
Beklenti: yetki kontrolleri ve response schema dogrulamasi.
```

## 9) Admin Panel E2E - User Management

```text
TestSprite: frontend-admin user management E2E testleri olustur.
Kapsam:
- Admin login (loginIdentifier)
- /admin/users (Zugriff & Rollen hub)
- Quick create / username
UI metinleri Almanca kalmali.
```

## 10) Admin Panel E2E - Backup hub

```text
TestSprite: frontend-admin backup hub E2E testleri olustur.
Kapsam:
- /backup (legacy /settings/backup-dr degil)
- /backup/costs, /backup/compliance
- Manual backup tetikleme (yetkiye gore)
```

## 11) Offline orders + Sites

```text
TestSprite: offline_orders ve public sites smoke testleri olustur.
Kapsam:
- GET /api/admin/offline-orders
- GET /api/pos/offline-orders/pending
- GET /api/sites/{slug}/status
- POST /api/public/online-orders (closed saatlerde ONLINE_ORDERS_CLOSED kabul)
Kurallar: offline_transactions ile karistirma; working hours POS/FA'yi kapatmaz.
```

## Opsiyonel Toplu Prompt (Tum Paket)

```text
TestSprite: Regkasse icin uctan uca test paketi calistir.
Sirayla:
1) Auth smoke (loginIdentifier, invalid → 401)
2) Tenant isolation (cross-tenant → 404)
3) Backup API (status, compliance, costs)
4) Offline orders + sites status
5) Admin E2E (/admin/users + /backup)
Cikti: basarisizlar, root cause, P0/P1/P2.
```
