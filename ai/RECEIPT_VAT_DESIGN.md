# Receipt VAT & Money Precision – Design

## 1. Rounding (tek nokta)

- **Kaynak:** `CartMoneyHelper.Round(decimal value)`
- **Strateji:** `Math.Round(value, 2, MidpointRounding.AwayFromZero)` (2 ondalık, EUR)
- **Kural:** Tüm fiş/ödeme tutarları sadece bu fonksiyonla yuvarlanır; başka yerde `Math.Round` kullanılmaz.

## 2. Satır hesabı (gross model)

- **Girdi:** `unitPriceGross`, `quantity`, `vatRate` (kesir: 0.10, 0.20)
- **Adımlar:**
  - `lineGross = Round(unitGross * qty)`
  - `lineNet = rate <= 0 ? lineGross : Round(lineGross / (1 + rate))`
  - `lineVat = Round(lineGross - lineNet)` → `lineNet + lineVat = lineGross` korunur
- **Kullanım:** Payment ve Receipt tarafında aynı `CartMoneyHelper.ComputeLine(unitGross, qty, vatRatePercent)` kullanılır.

## 3. Totals & breakdown

- **Totals:** Satırlardan toplam (yeniden bölme yok):
  - `totalNet = sum(line.LineNet)`
  - `totalVat = sum(line.LineTax)`
  - `totalGross = sum(line.LineGross)`
- **VAT breakdown:** `vatRate` (ve gerekiyorsa TaxType) ile grupla, her grupta Net/Tax/Gross topla.
- **Determinizm:** Aynı PaymentItems JSON → aynı Receipt totals ve tax lines.

## 4. ReceiptItem (model + DTO)

| Alan | Açıklama |
|------|----------|
| ProductName (name) | Ürün/ad |
| Quantity | Miktar |
| UnitPrice | Birim fiyat (gross) |
| TotalPrice | Satır toplam (gross) = lineTotalGross |
| LineNet | Satır net (yeni kolon) |
| VatAmount | Satır vergi (yeni kolon) |
| TaxRate | Vergi oranı % (10, 20) – mevcut |
| CategoryName | Kategori adı (opsiyonel, yeni) |

DTO: `lineTotalNet`, `lineTotalGross`, `vatRate` (kesir veya %), `vatAmount`, `categoryName`.

## 5. ReceiptTotals (DTO)

- `totalNet`, `totalVat`, `totalGross` (ReceiptDTO içinde mevcut SubTotal/TaxAmount/GrandTotal ile aynı; isim netleştirilebilir).

## 6. VatBreakdown (mevcut ReceiptTaxLineDTO)

- `vatRate` (%), `netAmount`, `vatAmount`, `grossAmount` (zaten var).
- İsteğe bağlı: `vatRateFraction` (0.10, 0.20) FE için.

## 7. Modifier VAT

- Şu an: Modifier satırı, ana ürünün vergi oranını kullanır (parent product VAT).
- İleride: Modifier kendi VAT oranına sahip olabilir; şimdilik parent VAT kullanılır.

## 8. Receipt JSON örneği (POS FE render için)

```json
{
  "receiptNumber": "AT-KASSE01-20260304-00001",
  "items": [
    { "name": "Döner", "quantity": 1, "unitPrice": 6.90, "lineTotalGross": 6.90, "lineTotalNet": 6.27, "vatRate": 0.10, "vatAmount": 0.63, "categoryName": "Speisen", "isModifierLine": false },
    { "name": "Extra Fleisch", "quantity": 1, "unitPrice": 1.50, "lineTotalGross": 1.50, "lineTotalNet": 1.36, "vatRate": 0.10, "vatAmount": 0.14, "isModifierLine": true },
    { "name": "Cola", "quantity": 1, "unitPrice": 2.50, "lineTotalGross": 2.50, "lineTotalNet": 2.08, "vatRate": 0.20, "vatAmount": 0.42, "categoryName": "Getränke", "isModifierLine": false }
  ],
  "totals": { "totalNet": 9.71, "totalVat": 1.19, "totalGross": 10.90 },
  "taxRates": [
    { "rate": 10, "vatRate": 0.10, "netAmount": 7.63, "taxAmount": 0.77, "grossAmount": 8.40 },
    { "rate": 20, "vatRate": 0.20, "netAmount": 2.08, "taxAmount": 0.42, "grossAmount": 2.50 }
  ]
}
```

Fiş metni: Döner €6.90 (VAT 10%) + Extra Fleisch €1.50 (VAT 10%); Cola €2.50 (VAT 20%). VAT Breakdown: 10% Net 7.63 / VAT 0.77 / Gross 8.40 | 20% Net 2.08 / VAT 0.42 / Gross 2.50
- İleride: Modifier kendi VAT’ına sahip olabilir; şimdilik parent VAT kullanılır.
