# Cart API – Fiyat ve Vergi Sözleşmesi

## Net/Gross Model Tanımı (Tek Cümle)

**POS'ta görünen fiyat ve müşterinin ödediği fiyat gross (KDV dahil).** Backend tek muhasebe modeli uygular; FE hiçbir vergi/total hesaplaması yapmaz, sadece backend alanlarını render eder.

## İş Kuralı

**Product.Price = GROSS (Bruttopreis, inkl. MwSt.)**

UI’da gösterilen tüm fiyatlar KDV dahildir. FE vergi hesaplamaz; sadece backend’den gelen değerleri render eder.

## Alan Semantiği

| Alan | Anlam | Örnek (€10 gross, 20%) |
|------|-------|-------------------------|
| `unitPrice` / `UnitPrice` | Birim fiyat (gross) | 10.00 |
| `totalPrice` / `TotalPrice` | Satır toplamı (gross) | 10.00 |
| `lineNet` / `LineNet` | Satır net tutarı | 8.33 |
| `lineTax` / `LineTax` | Satır içindeki vergi payı | 1.67 |
| `subtotalGross` | Bruttosumme | 10.00 |
| `subtotalNet` | Netto toplam | 8.33 |
| `includedTaxTotal` | Gömülü vergi toplamı | 1.67 |
| `grandTotalGross` | Ödenecek toplam | 10.00 |

## Vergi Hesaplama (Gross Model)

- `embeddedTax = gross * rate / (1 + rate)`
- `net = gross / (1 + rate)`
- Örnek: €10 gross, 20% → tax = 1.67, net = 8.33

## taxSummary (Vergi Grubu Özeti)

```json
{
  "taxSummary": [
    {
      "taxType": 1,
      "taxRatePct": 20,
      "netAmount": 8.33,
      "taxAmount": 1.67,
      "grossAmount": 10.00
    }
  ]
}
```

Çoklu vergi grubunda (örn. 20% + 10%) her grup için ayrı satır döner.

## Deprecated / Kaldırıldı

- `subtotal`, `totalTax`, `grandTotal` API response'tan kaldırıldı. Sadece gross model alanları kullanılır.

## Rounding

- `decimal(18,2)`
- `MidpointRounding.AwayFromZero`
