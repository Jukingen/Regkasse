using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace Registrierkasse.Services
{
    public interface IFinanzOnlineService
    {
        Task<bool> AuthenticateAsync();
        Task<bool> SubmitInvoiceAsync(FinanzOnlineInvoice invoice);
        Task<bool> SubmitDailyReportAsync(FinanzOnlineDailyReport report);
        Task<FinanzOnlineStatus> GetStatusAsync();
        Task<List<FinanzOnlineError>> GetErrorsAsync();
        Task<bool> ValidateTaxNumberAsync(string taxNumber);
    }

    public class FinanzOnlineService : IFinanzOnlineService
    {
        private readonly ILogger<FinanzOnlineService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _username;
        private readonly string _password;
        private string _accessToken;
        private DateTime _tokenExpiry;

        public FinanzOnlineService(ILogger<FinanzOnlineService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiUrl = Environment.GetEnvironmentVariable("FINANZONLINE_API_URL") ?? "https://finanzonline.bmf.gv.at/api";
            _username = Environment.GetEnvironmentVariable("FINANZONLINE_USERNAME") ?? "";
            _password = Environment.GetEnvironmentVariable("FINANZONLINE_PASSWORD") ?? "";
            _accessToken = string.Empty;
            _tokenExpiry = DateTime.MinValue;
        }

        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                _logger.LogInformation("FinanzOnline API'ye kimlik doğrulama yapılıyor...");

                var authRequest = new
                {
                    username = _username,
                    password = _password,
                    grant_type = "password"
                };

                var json = JsonSerializer.Serialize(authRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/auth/token", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent);
                    
                    if (authResponse != null && !string.IsNullOrEmpty(authResponse.AccessToken))
                    {
                        _accessToken = authResponse.AccessToken;
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(authResponse.ExpiresIn);
                        
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                        
                        _logger.LogInformation("FinanzOnline API kimlik doğrulama başarılı");
                        return true;
                    }
                }

                _logger.LogError("FinanzOnline API kimlik doğrulama başarısız");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline API kimlik doğrulama hatası");
                return false;
            }
        }

        public async Task<bool> SubmitInvoiceAsync(FinanzOnlineInvoice invoice)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    return false;
                }

                _logger.LogInformation($"Fatura gönderiliyor: {invoice.InvoiceNumber}");

                var json = JsonSerializer.Serialize(invoice);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/invoices", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var submitResponse = JsonSerializer.Deserialize<SubmitResponse>(responseContent);
                    
                    if (submitResponse != null && submitResponse.Success)
                    {
                        _logger.LogInformation($"Fatura başarıyla gönderildi: {invoice.InvoiceNumber}");
                        return true;
                    }
                }

                _logger.LogError($"Fatura gönderimi başarısız: {invoice.InvoiceNumber}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatura gönderimi hatası: {invoice.InvoiceNumber}");
                return false;
            }
        }

        public async Task<bool> SubmitDailyReportAsync(FinanzOnlineDailyReport report)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    return false;
                }

                _logger.LogInformation($"Günlük rapor gönderiliyor: {report.Date:yyyy-MM-dd}");

                var json = JsonSerializer.Serialize(report);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/daily-reports", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var submitResponse = JsonSerializer.Deserialize<SubmitResponse>(responseContent);
                    
                    if (submitResponse != null && submitResponse.Success)
                    {
                        _logger.LogInformation($"Günlük rapor başarıyla gönderildi: {report.Date:yyyy-MM-dd}");
                        return true;
                    }
                }

                _logger.LogError($"Günlük rapor gönderimi başarısız: {report.Date:yyyy-MM-dd}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Günlük rapor gönderimi hatası: {report.Date:yyyy-MM-dd}");
                return false;
            }
        }

        public async Task<FinanzOnlineStatus> GetStatusAsync()
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    return new FinanzOnlineStatus { IsConnected = false };
                }

                var response = await _httpClient.GetAsync($"{_apiUrl}/status");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<FinanzOnlineStatus>(responseContent);
                    
                    if (status != null)
                    {
                        status.IsConnected = true;
                        return status;
                    }
                }

                return new FinanzOnlineStatus { IsConnected = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline durum bilgisi alma hatası");
                return new FinanzOnlineStatus { IsConnected = false };
            }
        }

        public async Task<List<FinanzOnlineError>> GetErrorsAsync()
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    return new List<FinanzOnlineError>();
                }

                var response = await _httpClient.GetAsync($"{_apiUrl}/errors");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var errors = JsonSerializer.Deserialize<List<FinanzOnlineError>>(responseContent);
                    
                    return errors ?? new List<FinanzOnlineError>();
                }

                return new List<FinanzOnlineError>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline hata listesi alma hatası");
                return new List<FinanzOnlineError>();
            }
        }

        public async Task<bool> ValidateTaxNumberAsync(string taxNumber)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    return false;
                }

                var request = new { taxNumber };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/validate-tax-number", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var validationResponse = JsonSerializer.Deserialize<ValidationResponse>(responseContent);
                    
                    return validationResponse?.IsValid ?? false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vergi numarası doğrulama hatası");
                return false;
            }
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                return await AuthenticateAsync();
            }
            return true;
        }
    }

    public class FinanzOnlineInvoice
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string TaxNumber { get; set; } = string.Empty;
        public string TseSignature { get; set; } = string.Empty;
        public string CashRegisterId { get; set; } = string.Empty;
        public List<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        public decimal TotalNet { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalGross { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class InvoiceItem
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string TaxType { get; set; } = string.Empty; // standard, reduced, special
        public decimal TaxAmount { get; set; }
    }

    public class FinanzOnlineDailyReport
    {
        public DateTime Date { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string CashRegisterId { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal VoucherAmount { get; set; }
        public decimal TaxStandard { get; set; }
        public decimal TaxReduced { get; set; }
        public decimal TaxSpecial { get; set; }
    }

    public class FinanzOnlineStatus
    {
        public bool IsConnected { get; set; }
        public string ApiVersion { get; set; } = string.Empty;
        public DateTime LastSync { get; set; }
        public int PendingInvoices { get; set; }
        public int PendingReports { get; set; }
    }

    public class FinanzOnlineError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }

    public class SubmitResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
    }

    public class ValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 