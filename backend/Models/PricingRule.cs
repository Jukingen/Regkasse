using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Hospitality fiyat kuralı: zaman penceresi, gün maskesi, ürün/kategori hedefi, şube (kasa) kapsamı.
/// </summary>
[Table("pricing_rules")]
public class PricingRule
{
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Daha yüksek önce uygulanır (çakışma çözümü).</summary>
    [Column("priority")]
    public int Priority { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Kuralın geçerli olduğu ilk gün (Avusturya takvim günü, date).</summary>
    [Column("valid_from_date", TypeName = "date")]
    public DateOnly ValidFromDate { get; set; }

    /// <summary>Kuralın geçerli olduğu son gün (dahil).</summary>
    [Column("valid_to_date", TypeName = "date")]
    public DateOnly ValidToDate { get; set; }

    /// <summary>Bit maskesi: bit 0 = Pazar … bit 6 = Cumartesi (C# DayOfWeek ile uyumlu).</summary>
    [Column("days_of_week_mask")]
    public int DaysOfWeekMask { get; set; } = 0b1111111;

    [Column("time_window_enabled")]
    public bool TimeWindowEnabled { get; set; }

    /// <summary>Gün içi başlangıç (dakika 0–1439, yerel saat).</summary>
    [Column("time_start_minutes")]
    public int TimeStartMinutes { get; set; }

    /// <summary>Gün içi bitiş. time_start &gt; time_end ise geceyi aşan pencere (ör. 22:00–02:00).</summary>
    [Column("time_end_minutes")]
    public int TimeEndMinutes { get; set; } = 1439;

    [Column("target_scope")]
    public PricingRuleTargetScope TargetScope { get; set; }

    [Column("target_id")]
    public Guid TargetId { get; set; }

    [Column("action_type")]
    public PricingRuleActionType ActionType { get; set; }

    /// <summary>FixedGross: brüt EUR; PercentOffList: yüzde indirim (ör. 10 = %10).</summary>
    [Column("action_value", TypeName = "decimal(18,4)")]
    public decimal ActionValue { get; set; }

    /// <summary>Null = tüm şubeler/kasalar; dolu = yalnızca bu kasa.</summary>
    [Column("cash_register_id")]
    public Guid? CashRegisterId { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}

public enum PricingRuleTargetScope : byte
{
    Product = 0,
    Category = 1,
}

public enum PricingRuleActionType : byte
{
    /// <summary>Katalog brüt liste fiyatının yerine geçen sabit brüt birim fiyat.</summary>
    FixedGrossPrice = 0,
    /// <summary>Katalog fiyatından yüzde indirim (pozitif sayı = indirim oranı).</summary>
    PercentOffList = 1,
}
