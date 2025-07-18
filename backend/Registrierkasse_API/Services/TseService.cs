using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Models;
using System.Text;

namespace Registrierkasse_API.Services
{
    public interface ITseService
    {
        Task<bool> IsConnectedAsync();
        Task<string> GetTseIdAsync();
        Task<TseSignatureResult> SignTransactionAsync(string processData, string processType = "SIGN");
        Task<TseSignatureResult> SignDailyReportAsync();
        Task<TseSignatureResult> SignNullbelegAsync(DateTime date, string cashRegisterId);
        Task<bool> ValidateSignatureAsync(string signature, string processData);
        Task<TseStatus> GetStatusAsync();
        Task<bool> InitializeHardwareAsync();
        Task<bool> DisconnectHardwareAsync();
    }

    public class TseService : ITseService
    {
        private readonly ILogger<TseService> _logger;
        private readonly ITseHardwareService _hardwareService;
        private string _tseDeviceId;
        private string _tseSerialNumber;
        private bool _isInitialized;

        public TseService(ILogger<TseService> logger, ITseHardwareService hardwareService)
        {
            _logger = logger;
            _hardwareService = hardwareService;
            _tseDeviceId = Environment.GetEnvironmentVariable("TSE_DEVICE_ID") ?? "DEMO-TSE-001";
            _tseSerialNumber = Environment.GetEnvironmentVariable("TSE_SERIAL_NUMBER") ?? "DEMO-SN-001";
            _isInitialized = false;
        }

        public async Task<bool> InitializeHardwareAsync()
        {
            try
            {
                _logger.LogInformation("TSE donanımı başlatılıyor...");
                
                // Hardware'e bağlan
                bool connected = await _hardwareService.ConnectAsync();
                if (!connected)
                {
                    _logger.LogError("TSE donanımına bağlanılamadı");
                    return false;
                }

                // Sertifikayı doğrula
                bool certValid = await _hardwareService.ValidateCertificateAsync();
                if (!certValid)
                {
                    _logger.LogError("TSE sertifikası geçersiz");
                    return false;
                }

                // Seri numarasını al
                string serialNumber = await _hardwareService.GetSerialNumberAsync();
                if (!string.IsNullOrEmpty(serialNumber))
                {
                    _tseSerialNumber = serialNumber;
                }

                _isInitialized = true;
                _logger.LogInformation("TSE donanımı başarıyla başlatıldı");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE donanımı başlatma hatası");
                return false;
            }
        }

        public async Task<bool> DisconnectHardwareAsync()
        {
            try
            {
                return await _hardwareService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE donanımı bağlantısını kapatma hatası");
                return false;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                return await _hardwareService.IsConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE cihazı bağlantı kontrolü başarısız");
                return false;
            }
        }

        public async Task<string> GetTseIdAsync()
        {
            if (!_isInitialized)
            {
                await InitializeHardwareAsync();
            }
            return _tseDeviceId;
        }

        public async Task<TseSignatureResult> SignTransactionAsync(string processData, string processType = "SIGN")
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new TseException("TSE cihazı bağlı değil");
                }

                // Process data'yı byte array'e çevir
                byte[] dataToSign = Encoding.UTF8.GetBytes(processData);
                
                // Hardware ile imzala
                byte[] signatureBytes = await _hardwareService.SignDataAsync(dataToSign);
                
                // İmzayı hex string'e çevir
                string signature = Convert.ToHexString(signatureBytes);

                return new TseSignatureResult
                {
                    Signature = signature,
                    SignatureCounter = DateTime.UtcNow.Ticks,
                    Time = DateTime.UtcNow,
                    ProcessType = processType,
                    SerialNumber = _tseSerialNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE imzalama işlemi başarısız");
                throw new TseException("TSE imzalama işlemi başarısız", ex);
            }
        }

        public async Task<TseSignatureResult> SignDailyReportAsync()
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new TseException("TSE cihazı bağlı değil");
                }

                // Günlük rapor verisi oluştur
                string dailyReportData = $"DAILY_REPORT_{DateTime.UtcNow:yyyyMMdd}_{_tseSerialNumber}";
                byte[] dataToSign = Encoding.UTF8.GetBytes(dailyReportData);
                
                // Hardware ile imzala
                byte[] signatureBytes = await _hardwareService.SignDataAsync(dataToSign);
                string signature = Convert.ToHexString(signatureBytes);

                return new TseSignatureResult
                {
                    Signature = signature,
                    SignatureCounter = DateTime.UtcNow.Ticks,
                    Time = DateTime.UtcNow,
                    ProcessType = "DAILY_REPORT",
                    SerialNumber = _tseSerialNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE günlük rapor imzalama başarısız");
                throw new TseException("TSE günlük rapor imzalama başarısız", ex);
            }
        }

        public async Task<TseSignatureResult> SignNullbelegAsync(DateTime date, string cashRegisterId)
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new TseException("TSE cihazı bağlı değil");
                }

                // Nullbeleg işlemi için gerekli veri oluştur
                string nullbelegData = $"NULLBELEG_{date:yyyyMMdd}_{cashRegisterId}_{_tseSerialNumber}";
                byte[] dataToSign = Encoding.UTF8.GetBytes(nullbelegData);
                
                // Hardware ile imzala
                byte[] signatureBytes = await _hardwareService.SignDataAsync(dataToSign);
                string signature = Convert.ToHexString(signatureBytes);

                return new TseSignatureResult
                {
                    Signature = signature,
                    SignatureCounter = DateTime.UtcNow.Ticks,
                    Time = DateTime.UtcNow,
                    ProcessType = "NULLBELEG",
                    SerialNumber = _tseSerialNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE nullbeleg imzalama başarısız");
                throw new TseException("TSE nullbeleg imzalama başarısız", ex);
            }
        }

        public async Task<bool> ValidateSignatureAsync(string signature, string processData)
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new TseException("TSE cihazı bağlı değil");
                }

                // İmzayı byte array'e çevir
                byte[] signatureBytes = Convert.FromHexString(signature);
                byte[] dataBytes = Encoding.UTF8.GetBytes(processData);
                
                // Hardware ile doğrula
                // Bu kısım TSE cihazının doğrulama API'sine göre genişletilecek
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE imza doğrulama başarısız");
                return false;
            }
        }

        public async Task<TseStatus> GetStatusAsync()
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    return new TseStatus { IsConnected = false };
                }

                // Hardware durumunu al
                var hardwareStatus = await _hardwareService.GetHardwareStatusAsync();
                
                return new TseStatus
                {
                    IsConnected = hardwareStatus.IsConnected,
                    SerialNumber = _tseSerialNumber,
                    LastSignatureCounter = hardwareStatus.SignatureCounter,
                    LastSignatureTime = hardwareStatus.LastSignatureTime,
                    MemoryStatus = hardwareStatus.MemoryUsage > 80 ? "WARNING" : "OK",
                    CertificateStatus = hardwareStatus.CertificateValid ? "VALID" : "INVALID"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE durum bilgisi alınamadı");
                return new TseStatus { IsConnected = false };
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new TseException("TSE cihazı bağlı değil");
                }

                // Hardware başlatma
                bool initialized = await InitializeHardwareAsync();
                if (!initialized)
                {
                    throw new TseException("TSE donanımı başlatılamadı");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE başlatma işlemi başarısız");
                throw new TseException("TSE başlatma işlemi başarısız", ex);
            }
        }
    }

    public class TseSignatureResult
    {
        public string Signature { get; set; } = string.Empty;
        public long SignatureCounter { get; set; }
        public DateTime Time { get; set; }
        public string ProcessType { get; set; } = "SIGN";
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class TseStatus
    {
        public bool IsConnected { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public long LastSignatureCounter { get; set; }
        public DateTime LastSignatureTime { get; set; }
        public string MemoryStatus { get; set; } = string.Empty;
        public string CertificateStatus { get; set; } = string.Empty;
    }

    public class TseException : Exception
    {
        public TseException(string message) : base(message) { }
        public TseException(string message, Exception inner) : base(message, inner) { }
    }
} 
