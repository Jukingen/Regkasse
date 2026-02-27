using System;
using System.Linq;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services
{
    public class TseService : ITseService
    {
        private readonly AppDbContext _context;
        private readonly SignaturePipeline _pipeline;
        private readonly ITseKeyProvider _keyProvider;
        private readonly ILogger<TseService> _logger;

        public TseService(AppDbContext context, SignaturePipeline pipeline, ITseKeyProvider keyProvider, ILogger<TseService> logger)
        {
            _context = context;
            _pipeline = pipeline;
            _keyProvider = keyProvider;
            _logger = logger;
        }

        public async Task<TseStatus> GetTseStatusAsync()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return new TseStatus
                    {
                        IsConnected = false,
                        Status = "No TSE device found",
                        ErrorMessage = "No TSE device configured in the system"
                    };
                }

                return new TseStatus
                {
                    IsConnected = tseDevice.IsConnected,
                    DeviceId = tseDevice.Id.ToString(),
                    SerialNumber = tseDevice.SerialNumber,
                    IsOperational = tseDevice.CanCreateInvoices,
                    Status = tseDevice.IsConnected ? "Connected" : "Disconnected",
                    LastConnectionTime = tseDevice.LastFinanzOnlineSync,
                    ErrorMessage = tseDevice.IsConnected ? "" : "TSE device is not connected"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTseStatusAsync failed");
                return new TseStatus
                {
                    IsConnected = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<TseDevice> GetTseDeviceAsync(string deviceId)
        {
            if (Guid.TryParse(deviceId, out var guid))
            {
                return await _context.TseDevices.FindAsync(guid) ?? new TseDevice();
            }
            return new TseDevice();
        }

        public async Task<bool> ConnectTseDeviceAsync(string deviceId)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty) return false;
                device.IsConnected = true;
                device.LastFinanzOnlineSync = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> DisconnectTseDeviceAsync(string deviceId)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty) return false;
                device.IsConnected = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<TseSignatureResult> CreateInvoiceSignatureAsync(Guid cashRegisterId, string invoiceNumber, decimal totalAmount, string? kassenId = null, string? prevSignatureValue = null, DateTime? timestamp = null, string? taxDetailsJson = null)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            _logger.LogInformation("CreateInvoiceSignatureAsync started, correlationId={CorrelationId}, invoiceNumber={InvoiceNumber}", correlationId, invoiceNumber);

            var ts = timestamp ?? DateTime.UtcNow;
            var kId = kassenId ?? cashRegisterId.ToString();
            var prevSig = prevSignatureValue ?? await GetLastSignatureValueForKassenIdAsync(kId);

            var payload = new BelegdatenPayload
            {
                KassenId = kId,
                BelegNr = invoiceNumber,
                BelegDatum = ts.ToString("dd.MM.yyyy"),
                Uhrzeit = ts.ToString("HH:mm:ss"),
                Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                PrevSignatureValue = prevSig,
                TaxDetails = taxDetailsJson ?? "{}"
            };

            var compactJws = _pipeline.Sign(payload, correlationId);

            var tseSignature = new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = compactJws,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = invoiceNumber,
                Amount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = "Invoice",
                CertificateNumber = _keyProvider.GetCertificateSerialNumber()
            };

            _context.TseSignatures.Add(tseSignature);
            await _context.SaveChangesAsync();

            _logger.LogInformation("CreateInvoiceSignatureAsync completed, correlationId={CorrelationId}", correlationId);
            return new TseSignatureResult(compactJws, prevSig);
        }

        private async Task<string> GetLastSignatureValueForKassenIdAsync(string kassenId)
        {
            var lastReceipt = await _context.Receipts
                .Include(r => r.Payment)
                .Where(r => r.Payment != null && r.Payment.KassenId == kassenId && r.SignatureValue != null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastReceipt?.SignatureValue != null)
                return lastReceipt.SignatureValue;

            var lastPayment = await _context.PaymentDetails
                .Where(p => p.KassenId == kassenId && !string.IsNullOrEmpty(p.TseSignature))
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
            return lastPayment?.TseSignature ?? string.Empty;
        }

        public async Task<string> CreateDailyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var payload = new BelegdatenPayload
            {
                KassenId = cashRegisterId.ToString(),
                BelegNr = $"DAILY_{closingDate:yyyyMMdd}",
                BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                Uhrzeit = "23:59:59",
                Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                PrevSignatureValue = await GetLastSignatureValueForKassenIdAsync(cashRegisterId.ToString()),
                TaxDetails = "{}"
            };
            var compactJws = _pipeline.Sign(payload, correlationId);
            await StoreClosingSignatureAsync(cashRegisterId, compactJws, $"DAILY_{closingDate:yyyyMMdd}", totalAmount, "DailyClosing");
            return compactJws;
        }

        public async Task<string> CreateMonthlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var payload = new BelegdatenPayload
            {
                KassenId = cashRegisterId.ToString(),
                BelegNr = $"MONTHLY_{closingDate:yyyyMM}",
                BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                Uhrzeit = "23:59:59",
                Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                PrevSignatureValue = await GetLastSignatureValueForKassenIdAsync(cashRegisterId.ToString()),
                TaxDetails = "{}"
            };
            var compactJws = _pipeline.Sign(payload, correlationId);
            await StoreClosingSignatureAsync(cashRegisterId, compactJws, $"MONTHLY_{closingDate:yyyyMM}", totalAmount, "MonthlyClosing");
            return compactJws;
        }

        public async Task<string> CreateYearlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var payload = new BelegdatenPayload
            {
                KassenId = cashRegisterId.ToString(),
                BelegNr = $"YEARLY_{closingDate:yyyy}",
                BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                Uhrzeit = "23:59:59",
                Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                PrevSignatureValue = await GetLastSignatureValueForKassenIdAsync(cashRegisterId.ToString()),
                TaxDetails = "{}"
            };
            var compactJws = _pipeline.Sign(payload, correlationId);
            await StoreClosingSignatureAsync(cashRegisterId, compactJws, $"YEARLY_{closingDate:yyyy}", totalAmount, "YearlyClosing");
            return compactJws;
        }

        private async Task StoreClosingSignatureAsync(Guid cashRegisterId, string signature, string invoiceNumber, decimal amount, string type)
        {
            _context.TseSignatures.Add(new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = signature,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = invoiceNumber,
                Amount = amount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = type,
                CertificateNumber = _keyProvider.GetCertificateSerialNumber()
            });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ValidateTseSignatureAsync(string signature)
        {
            try
            {
                var dbRecord = await _context.TseSignatures.FirstOrDefaultAsync(t => t.Signature == signature);
                if (dbRecord != null) return true;

                if (_keyProvider is SoftwareTseKeyProvider softProvider)
                {
                    return _pipeline.Verify(signature, softProvider.GetPublicKey());
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<TseCertificateInfo> GetTseCertificateInfoAsync(string deviceId)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty)
                {
                    return new TseCertificateInfo { Status = "Device not found" };
                }

                var certBytes = _keyProvider.GetCertificateBytes();
                if (certBytes != null && certBytes.Length > 0)
                {
                    var parsed = CmcParser.ParseCertificate(certBytes);
                    return new TseCertificateInfo
                    {
                        CertificateNumber = parsed.SerialNumber,
                        ValidFrom = parsed.ValidFrom,
                        ValidUntil = parsed.ValidUntil,
                        Issuer = "TSE",
                        IsValid = parsed.IsValid,
                        Status = parsed.IsValid ? "Valid" : "Expired"
                    };
                }

                var serial = _keyProvider.GetCertificateSerialNumber();
                return new TseCertificateInfo
                {
                    CertificateNumber = serial ?? device.SerialNumber,
                    ValidFrom = DateTime.Today.AddYears(-1),
                    ValidUntil = DateTime.Today.AddYears(4),
                    Issuer = "Software TSE",
                    IsValid = true,
                    Status = "Valid"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetTseCertificateInfoAsync failed");
                return new TseCertificateInfo { Status = "Error retrieving certificate info" };
            }
        }

        public async Task<bool> BackupTseDataAsync(string deviceId)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                return device.Id != Guid.Empty;
            }
            catch { return false; }
        }

        public async Task<bool> RestoreTseDataAsync(string deviceId, byte[] backupData)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                return device.Id != Guid.Empty;
            }
            catch { return false; }
        }

        public async Task<TseStatus> GetDeviceStatusAsync()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return new TseStatus
                    {
                        IsConnected = false,
                        IsReady = false,
                        Status = "No TSE device found",
                        ErrorMessage = "No TSE device configured in the system"
                    };
                }

                return new TseStatus
                {
                    IsConnected = tseDevice.IsConnected,
                    IsReady = tseDevice.CanCreateInvoices && tseDevice.IsActive,
                    DeviceId = tseDevice.Id.ToString(),
                    SerialNumber = tseDevice.SerialNumber,
                    IsOperational = tseDevice.CanCreateInvoices,
                    Status = tseDevice.IsConnected ? "Connected" : "Disconnected",
                    LastConnectionTime = tseDevice.LastConnectionTime,
                    ErrorMessage = tseDevice.IsConnected ? "" : "TSE device is not connected"
                };
            }
            catch (Exception ex)
            {
                return new TseStatus
                {
                    IsConnected = false,
                    IsReady = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> CancelInvoiceSignatureAsync(string signature)
        {
            try
            {
                var tseSignature = await _context.TseSignatures.FirstOrDefaultAsync(t => t.Signature == signature);
                if (tseSignature == null) return false;
                tseSignature.IsValid = false;
                tseSignature.ValidationError = "Invoice cancellation requested";
                await _context.SaveChangesAsync();
                return true;
            }
            catch { return false; }
        }
    }
}
