# Security & Compliance

## Auth
- Backend: ASP.NET Core Identity + JWT (AuthController üzerinden)
- Çoğu endpoint [Authorize]
- Yeni endpoint eklerken varsayılan olarak authorize et; public ise özellikle belirt.

## Regülasyon / Kritik Alanlar
- TSE entegrasyonu (TseController)
- FinanzOnline hata/raporlama akışları (FinanzOnlineError)
- DailyClosing (gün sonu / kapanış)
- Receipt/GeneratedReceipt

## Golden Rules
- Bu modüllerde "mantık değiştirme" -> sadece istenen feature’ı ekle.
- Logging/Audit beklentisini koru.
- Money rounding ve receipt numbering gibi konularda varsayım yapma.
