# AI Task Template

## 0) Analiz önce (zorunlu)
- İlgili mevcut kod ve testleri oku; varsayım yapma.
- Kısa bulgu: mevcut davranış ne, ne değişecek, neden güvenli?

## 1) Hedef
- İstenen değişiklik:
- Kapsam dışı:

## 2) Etki alanı
- Backend: E/H
- Frontend POS: E/H
- Frontend Admin: E/H
- DB/Migration: E/H
- OpenAPI/Generated client: E/H
- Multi-tenant (host/slug, `tenant_id`, query filter, Super Admin): E/H

## 3) Risk notu
- Compliance/fiscal etkisi var mı?
- Auth/RBAC etkisi var mı?
- Kiracı izolasyonu / çapraz kiracı erişim etkisi var mı?
- Geriye dönük uyumluluk riski var mı?

## 4) Plan (kısa)
- Adım 1
- Adım 2
- Adım 3

## 5) Doğrulama
- **Hedefli testler** (fiscal alan: ilgili `KasseAPI_Final.Tests` filtreleri veya sözleşme scriptleri)
- Çalıştırılacak diğer script/komutlar (`verify-api-client`, OpenAPI kritik path, i18n vb.)
- Beklenen sonuç

## 6) Çıktı formatı
- **Etkilenen dosyalar** (tam yol veya repo-göreli net liste)
- Kısa gerekçe
- Test/script sonuçları
- **Risk özeti** (fiscal, auth, geriye dönük uyumluluk)
- Kalan belirsizlikler

## 7) Son denetim (final audit)
- Davranış değişikliği istenenle sınırlı mı?
- Swagger + Orval etkilendiyse senkron mu?
- Hassas alanlarda log/PII/voucher sızıntısı yok mu?
- Gerekirse `REGKASSE_AI_ONBOARDING.md` ve ilgili `/ai` maddeleriyle tutarlılık kontrolü
