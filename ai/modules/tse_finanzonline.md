# Module: TSE & FinanzOnline

## What this is
- TSE: cihaz / imza / kayıt akışları (TseDevice, TseSignature, TseController)
- FinanzOnline: hata ve raporlama (FinanzOnlineError)

## Rules
- Bu modüllerde davranış değişikliği yapma (breaking change yok)
- Sadece istenen endpoint/field ekle
- Hata durumlarını sessizce yutma; logla ve mevcut error yaklaşımını koru
