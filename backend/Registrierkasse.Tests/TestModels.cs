namespace Registrierkasse.Tests;

// Auth ile ilgili response modelleri
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
}

// TSE ile ilgili response modelleri
public class TseStatusResponse
{
    public bool IsConnected { get; set; }
    public string DeviceInfo { get; set; } = string.Empty;
}

public class DailyReportResponse
{
    public string Signature { get; set; } = string.Empty;
    public string ReportData { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

// Fatura ile ilgili response modelleri
public class InvoiceResponse
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string TseSignature { get; set; } = string.Empty;
    public bool IsPrinted { get; set; }
    public Dictionary<string, decimal> TaxDetails { get; set; } = new();
}

// Test request modelleri
public class InvoiceRequest
{
    public List<InvoiceItemRequest> Items { get; set; } = new();
    public PaymentRequest Payment { get; set; } = new();
}

public class InvoiceItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string TaxType { get; set; } = string.Empty;
}

public class PaymentRequest
{
    public string Method { get; set; } = string.Empty;
    public bool TseRequired { get; set; }
} 