# Users Feature – End-to-End Fix Summary

## 1. As-is analysis (kısa)

- **User details:** Drawer açılıyor; Activity sekmesinde audit log GET `/api/AuditLog/user/{userId}` çağrılıyordu. Tablo yoksa 500, ayrıca query key kararsız + retry/refetch ile log spam oluşuyordu.
- **User update:** PUT `/api/UserManagement/{id}` body’de `employeeNumber` opsiyonel; boş/null gelince entity’ye null atanıyor, DB `employee_number` NOT NULL → 500.
- **Reset password:** PUT `/api/UserManagement/{id}/reset-password` body `{ newPassword }` (camelCase). Backend DTO `NewPassword` (PascalCase); binding ortama göre bazen başarısız → 400.
- **Audit log query:** Params her render’da yeni obje → key değişiyor → gereksiz refetch; retry ve refetchOnWindowFocus ile hata durumunda tekrarlayan istek ve console log.

---

## 2. Etkilenen dosyalar

### Backend
| Dosya | Değişiklik |
|-------|------------|
| `backend/Data/AppDbContext.cs` | AuditLog için `ToTable("audit_logs")`. |
| `backend/Migrations/20260308175048_AddAuditLogsTable.cs` | `audit_logs` tablosu CREATE TABLE IF NOT EXISTS. |
| `backend/Migrations/AppDbContextModelSnapshot.cs` | AuditLog → `audit_logs`. |
| `backend/Controllers/UserManagementController.cs` | CreateUserRequest/UpdateUserRequest `EmployeeNumber` required; ResetPasswordRequest `[JsonPropertyName("newPassword")]` + açık validasyon; ResetPassword action’da net 400 mesajları. |

### Frontend
| Dosya | Değişiklik |
|-------|------------|
| `frontend-admin/src/features/users/components/UserActivityTimeline.tsx` | `useMemo` params, `retry: false`, `refetchOnWindowFocus: false`, `enabled: !!userId`, `errorLoadActivity`. |
| `frontend-admin/src/features/users/constants/validation.ts` | `employeeNumber` required kuralı. |
| `frontend-admin/src/features/users/constants/copy.ts` | `errorLoadActivity`. |
| `frontend-admin/src/app/(protected)/users/__tests__/page.test.tsx` | UserFormDrawer mock payload’a `employeeNumber: 'EMP001'`; create/edit testlerinde contract assertion. |

---

## 3. Hatalar (kategori bazlı)

### Data / migration
- **AuditLog 500:** Hiçbir migration `AuditLogs` tablosunu oluşturmuyordu; EF `"AuditLogs"` kullanıyordu.
- **Çözüm:** `ToTable("audit_logs")` + migration ile `audit_logs` CREATE TABLE IF NOT EXISTS.

### API contract
- **Update 500 employee_number:** DTO’da EmployeeNumber opsiyonel, entity’ye null atanıyordu; DB NOT NULL.
- **Çözüm:** CreateUserRequest ve UpdateUserRequest’te `EmployeeNumber` required (`[Required(AllowEmptyStrings = false)]`).
- **Reset 400:** JSON key `newPassword` ↔ C# `NewPassword` binding tutarsızlığı.
- **Çözüm:** `[JsonPropertyName("newPassword")]` + açık null/length kontrolü ve tek tip 400 cevabı.

### Frontend query behavior
- **Audit log spam / tekrar fetch:** Key her render’da değişiyor (params yeni obje); retry ve refetchOnWindowFocus açık.
- **Çözüm:** `params = useMemo(() => ({ page, pageSize }), [page])`, `retry: false`, `refetchOnWindowFocus: false`, `enabled: !!userId`.

### Validation / UX
- **employeeNumber:** Form’da required yoktu; backend 500 ile düşüyordu.
- **Çözüm:** `validation.ts` içinde `employeeNumber` required; copy’de `errorLoadActivity` ile audit paneli hatası ayrı mesaj.

---

## 4. Uygulanan düzeltmeler (özet)

- Backend: audit_logs tablosu + migration; EmployeeNumber required (create/update); ResetPasswordRequest JsonPropertyName + validasyon; net 400 mesajları.
- Frontend: Audit log query stabil key, retry/refetchOnWindowFocus kapatıldı, enabled koşulu; employeeNumber required; errorLoadActivity; test mock ve assertion’lar güncellendi.

---

## 5. Çalışır durum checklist

- [ ] **User details ekranı açılıyor** – Users listesinden kullanıcıya tıkla → detay drawer açılır.
- [ ] **Audit log bölümü** – Activity sekmesinde ya liste gelir ya da “Aktivitätsverlauf konnte nicht geladen werden.” + Erneut versuchen (kontrollü fallback); 500’de ekran bozulmaz.
- [ ] **User update 500 yok** – Edit drawer’da tüm alanlar (Mitarbeiternummer dahil) dolu; kaydet → 200, employee_number null hatası yok.
- [ ] **Employee number** – Create/Edit formda zorunlu; boş bırakılamaz; backend 400 döner.
- [ ] **Reset password çalışıyor** – Modal’da yeni şifre (min 6) gir → Speichern → 200; 400 contract hatası yok.
- [ ] **Gereksiz tekrar log/fetch yok** – Audit log hata aldığında tek istek, tek console.error; sayfa/focus değişince gereksiz refetch yok.
- [ ] **Temel testler** – `npm run test` (users page testleri) geçer; create/update mock’ları employeeNumber içerir; reset password testi `newPassword` ile çağrıyı doğrular.

---

## 6. Test komutları

```bash
# Backend migration (audit_logs)
cd backend && dotnet ef database update

# Frontend unit tests (users)
cd frontend-admin && npm run test -- --run src/app/\(protected\)/users/__tests__/page.test.tsx
```
