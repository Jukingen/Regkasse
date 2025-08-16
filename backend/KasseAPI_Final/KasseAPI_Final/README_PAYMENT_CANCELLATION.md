# Ödeme İptal Özelliği Kurulum ve Kullanım

## Türkçe Açıklama
Bu özellik, kasiyerlerin ödeme işlemini iptal etmesine olanak tanır. İptal edilen ödeme session'ı veritabanından silinir ve stok miktarları geri eklenir.

## Kurulum

### 1. Veritabanı Güncellemesi
PaymentSession tablosuna yeni alanları eklemek için aşağıdaki SQL script'i çalıştırın:

```sql
-- PostgreSQL'de çalıştırın
\i add-payment-cancellation-fields.sql
```

### 2. Backend Güncellemesi
- PaymentController'a `CancelPayment` endpoint'i eklendi
- PaymentSession modeline iptal alanları eklendi
- Gerekli response tipleri tanımlandı

### 3. Frontend Güncellemesi
- PaymentScreen'e ödeme iptal işlevi eklendi
- CartScreen'e iptal callback'i eklendi
- PaymentCancelResponse tipi tanımlandı

## API Endpoint

### POST /api/payment/cancel
Ödeme işlemini iptal eder.

**Request Body:**
```json
{
  "paymentSessionId": "string",
  "cancellationReason": "string (opsiyonel)"
}
```

**Response:**
```json
{
  "success": true,
  "paymentSessionId": "string",
  "cartId": "string",
  "cancelledAt": "2025-01-15T10:30:00Z",
  "cancelledBy": "user_id",
  "cancellationReason": "Kasiyer tarafından iptal edildi",
  "message": "Payment session cancelled successfully"
}
```

## Kullanım

### Frontend'de Ödeme İptali
```typescript
import { PaymentService } from '../services/api/paymentService';

const paymentService = new PaymentService();

// Ödeme iptal et
const cancelResponse = await paymentService.cancelPayment(
  sessionId, 
  'Müşteri istedi'
);

if (cancelResponse.success) {
  console.log('Ödeme iptal edildi:', cancelResponse);
}
```

### Backend'de İptal İşlemi
```csharp
[HttpPost("cancel")]
[Authorize(Roles = "Administrator,Manager,Cashier")]
public async Task<ActionResult<PaymentCancelResponse>> CancelPayment([FromBody] PaymentCancelRequest request)
{
    // Ödeme session'ını bul
    var paymentSession = await _context.PaymentSessions
        .Include(ps => ps.Cart)
        .ThenInclude(c => c.Items)
        .FirstOrDefaultAsync(ps => ps.SessionId == request.PaymentSessionId);

    // Session'ı iptal et
    paymentSession.Status = PaymentSessionStatus.Cancelled;
    paymentSession.CancelledAt = DateTime.UtcNow;
    paymentSession.CancelledBy = userId;
    paymentSession.CancellationReason = request.CancellationReason;

    // Stok miktarlarını geri ekle
    foreach (var item in paymentSession.Cart.Items)
    {
        var product = await _context.Products.FindAsync(item.ProductId);
        if (product != null)
        {
            product.StockQuantity += item.Quantity;
        }
    }

    await _context.SaveChangesAsync();
    
    return Ok(new PaymentCancelResponse { /* ... */ });
}
```

## Güvenlik

- Sadece Administrator, Manager ve Cashier rolleri ödeme iptal edebilir
- Tüm iptal işlemleri audit log'a kaydedilir
- İptal sebebi zorunlu değil ama önerilir

## Loglama

Her iptal işlemi için:
- PaymentLogEntry oluşturulur (Status: Cancelled)
- AuditLog kaydı eklenir (Action: PAYMENT_CANCEL)
- Console ve dosyaya log yazılır

## Hata Yönetimi

- Session bulunamazsa: 404 Not Found
- Session zaten iptal edilmişse: 400 Bad Request
- Session tamamlanmışsa: 400 Bad Request
- Genel hata durumunda: 500 Internal Server Error

## Test

### Unit Test
```bash
dotnet test --filter "PaymentController.CancelPayment"
```

### Integration Test
```bash
dotnet test --filter "PaymentCancellationIntegration"
```

## Notlar

- İptal edilen ödeme session'ları geri alınamaz
- Stok miktarları otomatik olarak geri eklenir
- TSE imzası gerektirmez (iptal işlemi)
- Offline modda da çalışır (PouchDB ile)
