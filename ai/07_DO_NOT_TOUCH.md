# Critical: DO NOT TOUCH

Aşağıdaki alanlar Projenin regülasyon (RKSV) ve finansal tutarlılığı için kritiktir. Bir görev kapsamında özellikle istenmedikçe bu alanların çalışma mantığını değiştirme.

## 1. Receipt Numbering (Fiş Numaralandırma)
- **Format**: `AT-{KassenID}-{BelegNr}`
- **Kural**: Sayısal ardışıklık bozulmamalıdır. Veritabanındaki `ReceiptNumber` üretim mantığına dokunma.

## 2. TSE Signature Flow (TSE İmza Akışları)
- **Dosya**: `TseController.cs`, `TseService.cs` (veya ilgili TSE servisleri)
- **Kritiklik**: Verinin TSE cihazına gönderilip imza (hash) alınması süreci yasaldır. Payload yapısını veya imza doğrulama adımlarını değiştirme.

## 3. DailyClosing Finalization (Gün Sonu Kapatma)
- **Dosya**: `DailyClosingService.cs`
- **İşlem**: Gün sonu alındığında sayaçlar sıfırlanır ve FinanzOnline'a (veya aracı kuruma) hazır rapor oluşturulur. Kapanış bakiyesi hesaplama algoritmasını değiştirme.

## 4. FinanzOnline Payload Mapping
- **Kritiklik**: FinanzOnline'a gönderilen JSON/XML şemaları sabittir. Enum değerlerini veya zorunlu alan isimlerini (örn: `Satz_Normal`) kafana göre isimlendirme.

## 5. Money Rounding (Para Yuvarlama)
- **Kural**: Proje genelinde `decimal(18,2)` ve `MidpointRounding.AwayFromZero` (veya mevcut tanımlı politika) kullanılır. Fiyat hesaplama fonksiyonlarını refactor ederken bu hassasiyeti bozma.

> [!WARNING]
> Bu kuralların ihlali yasal uyumluluk (RKSV) hatalarına yol açabilir. Emin değilsen "Assumptions" bölümünde belirt.
