using System;
using System.Threading.Tasks;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    public interface ITseService
    {
        Task<TseStatus> GetTseStatusAsync();
        Task<TseDevice> GetTseDeviceAsync(string deviceId);
        Task<bool> ConnectTseDeviceAsync(string deviceId);
        Task<bool> DisconnectTseDeviceAsync(string deviceId);
        Task<string> CreateInvoiceSignatureAsync(Guid cashRegisterId, string invoiceNumber, decimal totalAmount);
        Task<string> CreateDailyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount);
        Task<string> CreateMonthlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount);
        Task<string> CreateYearlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount);
        Task<bool> ValidateTseSignatureAsync(string signature);
        Task<TseCertificateInfo> GetTseCertificateInfoAsync(string deviceId);
        Task<bool> BackupTseDataAsync(string deviceId);
        Task<bool> RestoreTseDataAsync(string deviceId, byte[] backupData);
    }

    public class TseStatus
    {
        public bool IsConnected { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public bool IsOperational { get; set; }
        public string Status { get; set; } = string.Empty; // Connected, Disconnected, Error, Maintenance
        public DateTime LastConnectionTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class TseCertificateInfo
    {
        public string CertificateNumber { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string Status { get; set; } = string.Empty; // Valid, Expired, Revoked
    }
}
