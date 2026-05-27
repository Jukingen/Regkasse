namespace KasseAPI_Final.Models.Reports;

public sealed class PeakHourSlotDto
{
    public int Day { get; set; }
    public int Hour { get; set; }
    public int TransactionCount { get; set; }
}

public sealed class StaffingRecommendationDto
{
    public int Hour { get; set; }
    public int SuggestedStaff { get; set; }
}

public sealed class PeakHoursReportDto
{
    public Guid? CashRegisterId { get; set; }
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }
    /// <summary>7 rows (Mon=0 … Sun=6) × 24 hours. Vienna local time.</summary>
    public int[][] Heatmap { get; set; } = Array.Empty<int[]>();
    public int MaxCellCount { get; set; }
    public PeakHourSlotDto? BusiestHour { get; set; }
    public PeakHourSlotDto? QuietestHour { get; set; }
    public double AverageTransactionsPerHour { get; set; }
    public IReadOnlyList<StaffingRecommendationDto> RecommendedStaffingLevels { get; set; } =
        Array.Empty<StaffingRecommendationDto>();
}

public sealed class ProductMovementItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public double VelocityPerDay { get; set; }
}

public sealed class ProductMonthlySalesDto
{
    public string Month { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class ProductSeasonalTrendDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public IReadOnlyList<ProductMonthlySalesDto> MonthlySales { get; set; } = Array.Empty<ProductMonthlySalesDto>();
}
