# RKSV Verification Data Normalization Plan

**Tarih:** 2025-02-25  
**Kapsam:** İmza doğrulama verisinin normalize edilmesi, migration stratejisi, entity model değişiklikleri  
**Kurallar:** Migration geri alınabilir, eski kayıtlarla uyumluluk korunur

---

## 1. Mevcut Schema Özeti

| Tablo | İmza Alanları | İlişki |
|-------|---------------|--------|
| `payment_details` | TseSignature, PrevSignatureValueUsed | Ana ödeme imzası (primary) |
| `receipts` | signature_value, prev_signature_value | Payment'dan kopyalanır |
| `invoices` | TseSignature | Fatura imzası |
| `TseSignatures` | Signature, CertificateNumber | Legacy imza audit tablosu |
| `DailyClosings` | TseSignature | Gün sonu imzası |

---

## 2. Kısa Vade: Nullable Alanlar (Phase 1)

### 2.1 Eklenecek Alanlar

Tüm imza içeren tablolara **nullable** kolonlar eklenir. Eski kayıtlar `NULL` kalır.

| Alan | Tip | MaxLength | Açıklama |
|------|-----|-----------|----------|
| `signature_format` | varchar | 50 | Örn: "COMPACT_JWS", "JWS_FLATTENED" |
| `jws_header` | varchar | 1000 | Base64URL header (Checklist 2) |
| `jws_payload` | varchar | 4000 | Base64URL payload (Belegdaten) |
| `jws_signature` | varchar | 500 | Base64URL imza (Checklist 5) |
| `provider` | varchar | 50 | Örn: "fiskaly", "epson", "software" |
| `correlation_id` | varchar | 100 | Log/trace için benzersiz ID |

### 2.2 Etkilenen Tablolar

- `payment_details`
- `receipts`
- `invoices`
- `"TseSignatures"`
- `"DailyClosings"`

### 2.3 Migration Örneği (Geri Alınabilir)

```csharp
// Up
migrationBuilder.AddColumn<string>(name: "signature_format", table: "payment_details", 
    type: "character varying(50)", maxLength: 50, nullable: true);
migrationBuilder.AddColumn<string>(name: "jws_header", table: "payment_details", 
    type: "character varying(1000)", maxLength: 1000, nullable: true);
// ... diğer alanlar

// Down
migrationBuilder.DropColumn(name: "signature_format", table: "payment_details");
migrationBuilder.DropColumn(name: "jws_header", table: "payment_details");
// ...
```

### 2.4 Uyumluluk

- Mevcut `TseSignature` / `signature_value` alanları **değiştirilmez**
- Yeni alanlar **opsiyonel**; mevcut pipeline bu alanları doldurmayabilir (soft migration)
- `SignaturePipeline.Sign()` ve `VerifyDiagnostic()` çıktıları bu alanlara yazılabilir

---

## 3. Orta Vade: verification_run / verification_step (Phase 2)

### 3.1 Yeni Tablolar

#### verification_run

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | uuid | PK |
| entity_type | varchar(50) | "Payment", "Receipt", "Invoice", "DailyClosing" |
| entity_id | uuid | payment_id, receipt_id, invoice_id, daily_closing_id |
| compact_jws | varchar(2000) | Tam JWS string |
| overall_status | varchar(20) | PASS, FAIL, WARN |
| provider | varchar(50) | fiskaly, epson, software |
| correlation_id | varchar(100) | Trace ID |
| run_at | timestamptz | Doğrulama zamanı |
| created_at | timestamptz | |

#### verification_step

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | uuid | PK |
| verification_run_id | uuid | FK |
| step_id | int | 1=CMC, 2=JWS format, 3=Hash, 4=Signature verify, 5=Padding |
| step_name | varchar(100) | Human-readable ad |
| status | varchar(20) | PASS, FAIL, WARN |
| evidence | varchar(2000) | Teknik detay |
| created_at | timestamptz | |

### 3.2 İlişki

- `verification_run` → `verification_step` (1:N)
- `verification_run.entity_id` polymorphic (farklı tablolara referans)

### 3.3 Migration

- Yeni tablolar eklenir
- `payment_details`, `receipts` vb. tablolara referans için `verification_run_id` (nullable FK) eklenebilir (opsiyonel, soft link)

---

## 4. Uzun Vade: Index, Retention, Archive (Phase 3)

### 4.1 Index Stratejisi

| Tablo | Index | Amaç |
|-------|-------|------|
| payment_details | idx_payment_details_correlation_id | Trace lookup |
| payment_details | idx_payment_details_provider | Provider filtreleme |
| verification_run | idx_verification_run_entity | Entity lookup |
| verification_run | idx_verification_run_run_at | Zaman aralığı sorguları |
| verification_run | idx_verification_run_status | FAIL/WARN raporlama |

### 4.2 Retention Politikası

- `verification_run` / `verification_step`: Varsayılan **7 yıl** (RKSV / muhasebe saklama)
- Eski verification_run kayıtları: `run_at < (now - 7 years)` → arşiv adayı

### 4.3 Archive Stratejisi

1. **Cold storage:** 7 yıldan eski verification_run → CSV/Parquet export → object storage
2. **DB cleanup:** Arşivlenen kayıtlar silinebilir veya `archived_at` ile işaretlenebilir
3. **Read-through:** Arşiv okuma için ayrı API/export dosyası

---

## 5. Entity Model Değişiklikleri

### 5.1 PaymentDetails (payment_details)

```csharp
// Nullable - Phase 1
[MaxLength(50)]
public string? SignatureFormat { get; set; }

[MaxLength(1000)]
[Column("jws_header")]
public string? JwsHeader { get; set; }

[MaxLength(4000)]
[Column("jws_payload")]
public string? JwsPayload { get; set; }

[MaxLength(500)]
[Column("jws_signature")]
public string? JwsSignature { get; set; }

[MaxLength(50)]
public string? Provider { get; set; }

[MaxLength(100)]
[Column("correlation_id")]
public string? CorrelationId { get; set; }
```

### 5.2 Receipt (receipts)

```csharp
[MaxLength(50)]
[Column("signature_format")]
public string? SignatureFormat { get; set; }

[MaxLength(1000)]
[Column("jws_header")]
public string? JwsHeader { get; set; }

[MaxLength(4000)]
[Column("jws_payload")]
public string? JwsPayload { get; set; }

[MaxLength(500)]
[Column("jws_signature")]
public string? JwsSignature { get; set; }

[MaxLength(50)]
[Column("provider")]
public string? Provider { get; set; }

[MaxLength(100)]
[Column("correlation_id")]
public string? CorrelationId { get; set; }
```

### 5.3 Invoice (invoices)

```csharp
[MaxLength(50)]
public string? SignatureFormat { get; set; }

[MaxLength(1000)]
public string? JwsHeader { get; set; }

[MaxLength(4000)]
public string? JwsPayload { get; set; }

[MaxLength(500)]
public string? JwsSignature { get; set; }

[MaxLength(50)]
public string? Provider { get; set; }

[MaxLength(100)]
public string? CorrelationId { get; set; }
```

### 5.4 TseSignature (TseSignatures)

```csharp
[MaxLength(50)]
public string? SignatureFormat { get; set; }

[MaxLength(1000)]
public string? JwsHeader { get; set; }

[MaxLength(4000)]
public string? JwsPayload { get; set; }

[MaxLength(500)]
public string? JwsSignature { get; set; }

[MaxLength(50)]
public string? Provider { get; set; }

[MaxLength(100)]
public string? CorrelationId { get; set; }
```

### 5.5 DailyClosing (DailyClosings)

```csharp
[MaxLength(50)]
public string? SignatureFormat { get; set; }

[MaxLength(1000)]
public string? JwsHeader { get; set; }

[MaxLength(4000)]
public string? JwsPayload { get; set; }

[MaxLength(500)]
public string? JwsSignature { get; set; }

[MaxLength(50)]
public string? Provider { get; set; }

[MaxLength(100)]
public string? CorrelationId { get; set; }
```

### 5.6 Yeni Modeller (Phase 2)

```csharp
[Table("verification_runs")]
public class VerificationRun
{
    [Key] public Guid Id { get; set; }
    [Required][MaxLength(50)] public string EntityType { get; set; } = string.Empty;
    [Required] public Guid EntityId { get; set; }
    [MaxLength(2000)] public string? CompactJws { get; set; }
    [Required][MaxLength(20)] public string OverallStatus { get; set; } = string.Empty;
    [MaxLength(50)] public string? Provider { get; set; }
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [Required] public DateTime RunAt { get; set; }
    [Required] public DateTime CreatedAt { get; set; }
    public virtual ICollection<VerificationStep> Steps { get; set; } = new List<VerificationStep>();
}

[Table("verification_steps")]
public class VerificationStep
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid VerificationRunId { get; set; }
    [Required] public int StepId { get; set; }
    [Required][MaxLength(100)] public string StepName { get; set; } = string.Empty;
    [Required][MaxLength(20)] public string Status { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Evidence { get; set; }
    [Required] public DateTime CreatedAt { get; set; }
    [ForeignKey("VerificationRunId")]
    public virtual VerificationRun? VerificationRun { get; set; }
}
```

---

## 6. Uygulama Sırası

| Adım | İşlem | Rollback |
|------|-------|----------|
| 1 | Migration: Add RKSV verification columns (Phase 1) | Down migration ile kolonlar kaldırılır |
| 2 | Entity modellere property ekle | - |
| 3 | AppDbContext Fluent API güncelle | - |
| 4 | SignaturePipeline / PaymentService: Yeni alanları doldur (opsiyonel) | - |
| 5 | Phase 2: verification_run, verification_step migration | Down ile tablolar drop |
| 6 | Phase 3: Index migration, retention job (ayrı task) | - |

---

## 7. Risk ve Uyumluluk Notları

- **RKSV §6:** TseSignature (tam JWS) zorunlu kalır; yeni alanlar ek bilgi.
- **07_DO_NOT_TOUCH:** Receipt numbering, TSE signature chain, daily closing logic dokunulmaz.
- **Backward compatibility:** Eski uygulama sürümleri yeni kolonları okumaz/yazmaz; NULL kabul edilir.
- **Rollback:** Tüm migration'lar `Down()` ile geri alınabilir.
