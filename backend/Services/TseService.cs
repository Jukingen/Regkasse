using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        public async Task<TseSignatureResult> CreateInvoiceSignatureAsync(Guid cashRegisterId, string invoiceNumber, decimal totalAmount, string registerNumber, string? prevSignatureValue = null, DateTime? timestamp = null, string? taxDetailsJson = null)
        {
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("cashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("registerNumber (fiscal Kassen-ID) is required.", nameof(registerNumber));

            var correlationId = Guid.NewGuid().ToString("N")[..12];
            _logger.LogInformation("CreateInvoiceSignatureAsync started, correlationId={CorrelationId}, invoiceNumber={InvoiceNumber}", correlationId, invoiceNumber);

            var ts = timestamp ?? DateTime.UtcNow;
            var kId = registerNumber.Trim();
            string compactJws = string.Empty;
            string prevSig = string.Empty;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (prevSigLocked, _) = await EnsureChainRowAndLockAsync(transaction, cashRegisterId);
                prevSig = prevSignatureValue ?? prevSigLocked;

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

                compactJws = _pipeline.Sign(payload, correlationId);

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
                await UpdateChainWithNewSignatureAsync(transaction, cashRegisterId, compactJws);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            _logger.LogInformation("CreateInvoiceSignatureAsync completed, correlationId={CorrelationId}", correlationId);
            return new TseSignatureResult(compactJws, prevSig);
        }

        /// <summary>Ensures a row exists for the register UUID, locks it (FOR UPDATE), and returns current last signature and counter.</summary>
        private async Task<(string prevSignature, int lastCounter)> EnsureChainRowAndLockAsync(IDbContextTransaction transaction, Guid cashRegisterId)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            await using var ensureCmd = conn.CreateCommand();
            ensureCmd.Transaction = transaction.GetDbTransaction();
            ensureCmd.CommandText = """
                INSERT INTO signature_chain_state (id, cash_register_id, last_signature, last_counter, updated_at)
                VALUES (gen_random_uuid(), @p0, NULL, 0, NOW())
                ON CONFLICT (cash_register_id) DO NOTHING
                """;
            var p0 = ensureCmd.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = cashRegisterId;
            ensureCmd.Parameters.Add(p0);
            await ensureCmd.ExecuteNonQueryAsync();

            await using var selectCmd = conn.CreateCommand();
            selectCmd.Transaction = transaction.GetDbTransaction();
            selectCmd.CommandText = """
                SELECT last_signature, last_counter FROM signature_chain_state WHERE cash_register_id = @p0 FOR UPDATE
                """;
            var p1 = selectCmd.CreateParameter();
            p1.ParameterName = "@p0";
            p1.Value = cashRegisterId;
            selectCmd.Parameters.Add(p1);
            using var reader = await selectCmd.ExecuteReaderAsync();
            string? prevSignature = null;
            var lastCounter = 0;
            if (await reader.ReadAsync())
            {
                prevSignature = reader.IsDBNull(0) ? null : reader.GetString(0);
                lastCounter = reader.GetInt32(1);
            }
            await reader.CloseAsync();
            return (prevSignature ?? string.Empty, lastCounter);
        }

        /// <summary>Updates the chain state with the new signature. Call within the same transaction after generating the signature.</summary>
        private async Task UpdateChainWithNewSignatureAsync(IDbContextTransaction transaction, Guid cashRegisterId, string newSignature)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction.GetDbTransaction();
            cmd.CommandText = """
                UPDATE signature_chain_state SET last_signature = @p0, last_counter = last_counter + 1, updated_at = NOW() WHERE cash_register_id = @p1
                """;
            var p0 = cmd.CreateParameter();
            p0.ParameterName = "@p0";
            p0.Value = newSignature;
            cmd.Parameters.Add(p0);
            var p1 = cmd.CreateParameter();
            p1.ParameterName = "@p1";
            p1.Value = cashRegisterId;
            cmd.Parameters.Add(p1);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string> CreateDailyClosingSignatureAsync(Guid cashRegisterId, string registerNumber, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("cashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("registerNumber is required.", nameof(registerNumber));
            var kId = registerNumber.Trim();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (prevSig, _) = await EnsureChainRowAndLockAsync(transaction, cashRegisterId);
                var correlationId = Guid.NewGuid().ToString("N")[..12];
                var payload = new BelegdatenPayload
                {
                    KassenId = kId,
                    BelegNr = $"DAILY_{closingDate:yyyyMMdd}",
                    BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                    Uhrzeit = "23:59:59",
                    Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    PrevSignatureValue = prevSig,
                    TaxDetails = "{}"
                };
                var compactJws = _pipeline.Sign(payload, correlationId);
                _context.TseSignatures.Add(new TseSignature
                {
                    Id = Guid.NewGuid(),
                    Signature = compactJws,
                    CashRegisterId = cashRegisterId,
                    InvoiceNumber = $"DAILY_{closingDate:yyyyMMdd}",
                    Amount = totalAmount,
                    CreatedAt = DateTime.UtcNow,
                    SignatureType = "DailyClosing",
                    CertificateNumber = _keyProvider.GetCertificateSerialNumber()
                });
                await UpdateChainWithNewSignatureAsync(transaction, cashRegisterId, compactJws);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return compactJws;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<string> CreateMonthlyClosingSignatureAsync(Guid cashRegisterId, string registerNumber, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("cashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("registerNumber is required.", nameof(registerNumber));
            var kId = registerNumber.Trim();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (prevSig, _) = await EnsureChainRowAndLockAsync(transaction, cashRegisterId);
                var correlationId = Guid.NewGuid().ToString("N")[..12];
                var payload = new BelegdatenPayload
                {
                    KassenId = kId,
                    BelegNr = $"MONTHLY_{closingDate:yyyyMM}",
                    BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                    Uhrzeit = "23:59:59",
                    Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    PrevSignatureValue = prevSig,
                    TaxDetails = "{}"
                };
                var compactJws = _pipeline.Sign(payload, correlationId);
                _context.TseSignatures.Add(new TseSignature
                {
                    Id = Guid.NewGuid(),
                    Signature = compactJws,
                    CashRegisterId = cashRegisterId,
                    InvoiceNumber = $"MONTHLY_{closingDate:yyyyMM}",
                    Amount = totalAmount,
                    CreatedAt = DateTime.UtcNow,
                    SignatureType = "MonthlyClosing",
                    CertificateNumber = _keyProvider.GetCertificateSerialNumber()
                });
                await UpdateChainWithNewSignatureAsync(transaction, cashRegisterId, compactJws);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return compactJws;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<string> CreateYearlyClosingSignatureAsync(Guid cashRegisterId, string registerNumber, DateTime closingDate, decimal totalAmount, int transactionCount)
        {
            if (cashRegisterId == Guid.Empty)
                throw new ArgumentException("cashRegisterId must not be empty.", nameof(cashRegisterId));
            if (string.IsNullOrWhiteSpace(registerNumber))
                throw new ArgumentException("registerNumber is required.", nameof(registerNumber));
            var kId = registerNumber.Trim();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (prevSig, _) = await EnsureChainRowAndLockAsync(transaction, cashRegisterId);
                var correlationId = Guid.NewGuid().ToString("N")[..12];
                var payload = new BelegdatenPayload
                {
                    KassenId = kId,
                    BelegNr = $"YEARLY_{closingDate:yyyy}",
                    BelegDatum = closingDate.ToString("dd.MM.yyyy"),
                    Uhrzeit = "23:59:59",
                    Betrag = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    PrevSignatureValue = prevSig,
                    TaxDetails = "{}"
                };
                var compactJws = _pipeline.Sign(payload, correlationId);
                _context.TseSignatures.Add(new TseSignature
                {
                    Id = Guid.NewGuid(),
                    Signature = compactJws,
                    CashRegisterId = cashRegisterId,
                    InvoiceNumber = $"YEARLY_{closingDate:yyyy}",
                    Amount = totalAmount,
                    CreatedAt = DateTime.UtcNow,
                    SignatureType = "YearlyClosing",
                    CertificateNumber = _keyProvider.GetCertificateSerialNumber()
                });
                await UpdateChainWithNewSignatureAsync(transaction, cashRegisterId, compactJws);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return compactJws;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
