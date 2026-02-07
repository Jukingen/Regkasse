using System;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services
{
    public class TseService : ITseService
    {
        private readonly AppDbContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public TseService(AppDbContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
        }

        private bool IsMockEnabled => _configuration.GetValue<bool>("Tse:MockEnabled") && !_env.IsProduction();

        public async Task<TseStatus> GetTseStatusAsync()
        {
            try
            {
                // MOCK CHECK
                if (IsMockEnabled)
                {
                    // Check if we have a real device, if not, return mock status
                    var realDevice = await _context.TseDevices.FirstOrDefaultAsync();
                    if (realDevice == null)
                    {
                        return new TseStatus
                        {
                            IsConnected = true,
                            DeviceId = "MOCK_DEVICE",
                            SerialNumber = "MOCK_SERIAL_123",
                            IsOperational = true,
                            Status = "Connected (MOCK)",
                            LastConnectionTime = DateTime.UtcNow,
                            ErrorMessage = ""
                        };
                    }
                }

                // Get the first available TSE device
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
                if (device.Id == Guid.Empty)
                {
                    return false;
                }

                device.IsConnected = true;
                device.LastFinanzOnlineSync = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DisconnectTseDeviceAsync(string deviceId)
        {
            try
            {
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty)
                {
                    return false;
                }

                device.IsConnected = false;
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> CreateInvoiceSignatureAsync(Guid cashRegisterId, string invoiceNumber, decimal totalAmount)
        {
            // MOCK SIGNATURE
            if (IsMockEnabled)
            {
                 var mockTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                 return $"MOCK_{cashRegisterId}_{invoiceNumber}_{totalAmount:F2}_{mockTimestamp}";
            }

            // Simulate TSE signature creation
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var signature = $"TSE_{cashRegisterId}_{invoiceNumber}_{totalAmount:F2}_{timestamp}";
            
            // Store signature in database
            var tseSignature = new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = signature,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = invoiceNumber,
                Amount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = "Invoice"
            };

            _context.TseSignatures.Add(tseSignature);
            await _context.SaveChangesAsync();

            return signature;
        }

        public async Task<string> CreateDailyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            // Simulate TSE daily closing signature
            var timestamp = closingDate.ToString("yyyyMMdd");
            var signature = $"TSE_DAILY_{cashRegisterId}_{timestamp}_{totalAmount:F2}_{transactionCount}";
            
            // Store signature in database
            var tseSignature = new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = signature,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = $"DAILY_{timestamp}",
                Amount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = "DailyClosing"
            };

            _context.TseSignatures.Add(tseSignature);
            await _context.SaveChangesAsync();

            return signature;
        }

        public async Task<string> CreateMonthlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            // Simulate TSE monthly closing signature
            var timestamp = closingDate.ToString("yyyyMM");
            var signature = $"TSE_MONTHLY_{cashRegisterId}_{timestamp}_{totalAmount:F2}_{transactionCount}";
            
            // Store signature in database
            var tseSignature = new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = signature,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = $"MONTHLY_{timestamp}",
                Amount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = "MonthlyClosing"
            };

            _context.TseSignatures.Add(tseSignature);
            await _context.SaveChangesAsync();

            return signature;
        }

        public async Task<string> CreateYearlyClosingSignatureAsync(Guid cashRegisterId, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            // Simulate TSE yearly closing signature
            var timestamp = closingDate.ToString("yyyy");
            var signature = $"TSE_YEARLY_{cashRegisterId}_{timestamp}_{totalAmount:F2}_{transactionCount}";
            
            // Store signature in database
            var tseSignature = new TseSignature
            {
                Id = Guid.NewGuid(),
                Signature = signature,
                CashRegisterId = cashRegisterId,
                InvoiceNumber = $"YEARLY_{timestamp}",
                Amount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                SignatureType = "YearlyClosing"
            };

            _context.TseSignatures.Add(tseSignature);
            await _context.SaveChangesAsync();

            return signature;
        }

        public async Task<bool> ValidateTseSignatureAsync(string signature)
        {
            try
            {
                var tseSignature = await _context.TseSignatures
                    .FirstOrDefaultAsync(t => t.Signature == signature);

                return tseSignature != null;
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
                    return new TseCertificateInfo
                    {
                        Status = "Device not found"
                    };
                }

                // Simulate certificate info
                return new TseCertificateInfo
                {
                    CertificateNumber = $"CERT_{device.SerialNumber}",
                    ValidFrom = DateTime.Today.AddYears(-1),
                    ValidUntil = DateTime.Today.AddYears(4),
                    Issuer = "Austrian TSE Authority",
                    IsValid = DateTime.Today <= DateTime.Today.AddYears(4),
                    Status = DateTime.Today <= DateTime.Today.AddYears(4) ? "Valid" : "Expired"
                };
            }
            catch
            {
                return new TseCertificateInfo
                {
                    Status = "Error retrieving certificate info"
                };
            }
        }

        public async Task<bool> BackupTseDataAsync(string deviceId)
        {
            try
            {
                // Simulate TSE data backup
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty)
                {
                    return false;
                }

                // In a real implementation, this would backup TSE data
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RestoreTseDataAsync(string deviceId, byte[] backupData)
        {
            try
            {
                // Simulate TSE data restore
                var device = await GetTseDeviceAsync(deviceId);
                if (device.Id == Guid.Empty)
                {
                    return false;
                }

                // In a real implementation, this would restore TSE data
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Yeni metodlar - interface'de tanımlanan eksik metodlar
        public async Task<TseStatus> GetDeviceStatusAsync()
        {
            try
            {
                // MOCK CHECK
                if (IsMockEnabled)
                {
                    // Check if we have a real device, if not, return mock status
                    var realDevice = await _context.TseDevices.FirstOrDefaultAsync();
                    if (realDevice == null)
                    {
                        return new TseStatus
                        {
                            IsConnected = true,
                            IsReady = true,
                            DeviceId = "MOCK_DEVICE",
                            SerialNumber = "MOCK_SERIAL_123",
                            IsOperational = true,
                            Status = "Connected (MOCK)",
                            LastConnectionTime = DateTime.UtcNow,
                            ErrorMessage = ""
                        };
                    }
                }

                // Get the first available TSE device
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
                // Find the TSE signature to cancel
                var tseSignature = await _context.TseSignatures
                    .FirstOrDefaultAsync(t => t.Signature == signature);

                if (tseSignature == null)
                {
                    return false;
                }

                // Mark the signature as invalid (cancelled)
                tseSignature.IsValid = false;
                tseSignature.ValidationError = "Invoice cancellation requested";

                await _context.SaveChangesAsync();

                // Log the cancellation for audit purposes
                // In a real implementation, this would also notify the TSE device

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
