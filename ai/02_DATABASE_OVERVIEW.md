# Database Overview

Bu dosya, şema detayını değil sistemin ana veri bölgelerini hızlı anlamak için özet sunar.

## Ana veri bölgeleri
- Satış çekirdeği: `Product`, `Category`, `Cart`, `CartItem`, `Order`, `OrderItem`, `PaymentDetails`.
- Fiş/fiscal katman: `Receipt*`, `ReceiptSequence`, `SignatureChainState`, `TseDevice`, `TseSignature`, `DailyClosing`.
- Kimlik ve oturum: `ApplicationUser` + `auth_sessions` + `refresh_tokens`.
- Finans entegrasyonu: `FinanzOnlineError`, `FinanzOnlineSubmission`, `FinanzOnlineOutboxMessage`.
- Operasyonel güvence: backup/restore verification tabloları.

## Operasyonel akış (özet)
1. POS sepet oluşturur/günceller.
2. Ödeme tamamlanır, receipt/fiscal kayıtları oluşur.
3. Gün sonu ve rapor tabloları (`Tagesbericht/Monatsbericht/Jahresbericht`) beslenir.
4. FinanzOnline/outbox süreçleri asenkron takip edilir.

## AI notları
- Şema hakkında karar verirken bu dosya yerine `AppDbContext` + ilgili migration dosyalarını referans al.
- Fiscal ve audit tablolarında “refactor” amaçlı değişiklikten kaçın; net ihtiyaç olmadan dokunma.
