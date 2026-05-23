# Tenant deletion — manual test checklist

Super Admin tenant lifecycle (soft delete, restore, permanent delete) and dev switcher behavior.

## Prerequisites

- Backend running locally (`http://localhost:5184` or configured API URL).
- Frontend Admin in development (`npm run dev` in `frontend-admin/`).
- Super Admin account and a separate Manager account.
- At least one disposable test tenant (no fiscal payments, no cash registers if testing hard delete).

## Checklist

- [ ] Super Admin olarak giriş yap
- [ ] "Silinmiş tenant'ları göster" toggle'ı çalışıyor (`/admin/tenants` → **Gelöschte anzeigen**; dev header switcher’da Super Admin için aynı filtre)
- [ ] Aktif tenant'ı soft-delete et → listede gri gözüküyor, "Geri Yükle" butonu var
- [ ] Soft-deleted tenant'ı geri yükle → normal görünüme dönüyor
- [ ] Soft-deleted tenant'ı kalıcı sil butonu ile dene → tenant **slug** yazınca onay aktif oluyor
- [ ] Kalıcı sil işlemi sonrası tenant listeden tamamen kayboluyor (`includeDeleted` kapalıyken)
- [ ] Manager rolünde bu butonlar görünmüyor (liste aksiyonları `—`; API 403)
- [ ] Tenant detail sayfasında Danger Zone görünüyor/çalışıyor (`/admin/tenants/{id}` → Settings → Gefahrenzone)

## Optional API smoke (curl)

```bash
# Soft delete (Super Admin JWT)
curl -X DELETE -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5184/api/admin/tenants/{tenantId}"

# Restore
curl -X POST -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5184/api/admin/tenants/{tenantId}/restore"

# Permanent delete (body: confirmSlug = tenant slug)
curl -X DELETE -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"confirmSlug\":\"tenant-slug\"}" \
  "http://localhost:5184/api/admin/tenants/{tenantId}/permanent"
```

## Automated tests

- Backend: `dotnet test --filter FullyQualifiedName~AdminTenantsControllerTests`
- Frontend Admin: `npm test -- src/app/(protected)/admin/tenants/__tests__/page.test.tsx`
