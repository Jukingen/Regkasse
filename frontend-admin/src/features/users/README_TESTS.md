# Users Modülü Test Paketi

## Kapsam

- **`hooks/__tests__/useUsersList.test.ts`** – Liste hook: başarılı/boş/hatalı yükleme, parametre iletimi (role, isActive, query, page, pageSize), `enabled: false` ile çağrı yok.
- **`app/(protected)/users/__tests__/page.test.tsx`** – Users sayfası: liste, filtreler, create/edit, deactivate/reactivate, reset password, yetki bazlı buton görünürlüğü.

## Çalıştırma

```bash
npm run test
# veya watch
npm run test:watch
```

## Mock’lar

- **usersGateway**: `getUsersList`, `createUser`, `updateUser`, `deactivateUser`, `reactivateUser`, `resetPassword`, `createRole`, `normalizeError` – gerçek endpoint şekilleri (`UserInfo`, `UsersListResponse`) kullanılır.
- **useAuth**: `{ user: { id, role: 'Admin' } }`
- **useUsersPolicy**: Varsayılan tam yetki; permission testinde `canCreate: false` override.
- **UserFormDrawer / UserDetailDrawer**: Basit stub (form submit ve kapatma).

## Davranış odaklı senaryolar

| Senaryo | Beklenen davranış |
|--------|---------------------|
| List load success | Tabloda kullanıcılar, email/role görünür |
| List empty | "Keine Benutzer gefunden." |
| List error | Hata metni + "Erneut versuchen" butonu |
| Filter default | İlk çağrıda `page: 1`, `pageSize: 20`, `isActive: true` |
| Search | Arama gönderilince `query` ile tekrar çağrı |
| Create success | `createUser` çağrılır, "Benutzer angelegt." |
| Create error | `message.error` çağrılır |
| Edit submit | `updateUser(id, data)` çağrılır |
| Deactivate | Modal açılır, reason girilir, `deactivateUser(id, { reason })` |
| Reactivate | Modal açılır, onayda `reactivateUser(id, undefined)` |
| Reset password kısa | Modal açık kalır, `resetPassword` çağrılmaz |
| Reset password geçerli | `resetPassword(id, { newPassword })` + success mesajı |
| canCreate false | "Benutzer anlegen" butonu yok |

## Flakiness önleme

- `retry: false` (QueryClient) ile ağ yeniden denemesi kapalı.
- `testTimeout: 15000` (vitest.config) ile yavaş ortamlarda zaman aşımı azaltılır.
- Buton seçicilerde Ant Design ikon+metin birleşik isim için regex kullanılır (örn. `/Bearbeiten/`).
