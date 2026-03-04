using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services
{
    public interface IReceiptService
    {
        Task<ReceiptDTO?> GetReceiptAsync(Guid receiptId);
        Task<ReceiptDTO> CreateReceiptFromPaymentAsync(Guid paymentId);
    }

    public class ReceiptService : IReceiptService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReceiptService> _logger;
        private readonly ITseService _tseService;
        private readonly CompanyProfileOptions _companyProfile;

        public ReceiptService(AppDbContext context, ILogger<ReceiptService> logger, ITseService tseService, Microsoft.Extensions.Options.IOptions<CompanyProfileOptions> companyProfile)
        {
            _context = context;
            _logger = logger;
            _tseService = tseService;
            _companyProfile = companyProfile.Value;
        }

        public async Task<ReceiptDTO?> GetReceiptAsync(Guid receiptId)
        {
            // Try to find existing receipt by ReceiptId OR PaymentId
            var receipt = await _context.Receipts
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.ReceiptId == receiptId || r.PaymentId == receiptId);

            if (receipt != null)
            {
                return MapToDTO(receipt);
            }

            // Fallback: If receipt doesn't exist but payment does, creating it on the fly?
            // Valid strategy if we want to generate receipts on demand for old payments.
            // Check if payment exists
            var paymentExists = await _context.PaymentDetails.AnyAsync(p => p.Id == receiptId);
            if (paymentExists)
            {
                return await CreateReceiptFromPaymentAsync(receiptId);
            }

            return null;
        }

        public async Task<ReceiptDTO> CreateReceiptFromPaymentAsync(Guid paymentId)
        {
            // 1. Check if receipt already exists
            var existingReceipt = await _context.Receipts
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

            if (existingReceipt != null)
            {
                return MapToDTO(existingReceipt);
            }

            // 2. Fetch payment (using repository or context)
            var payment = await _context.PaymentDetails.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) throw new KeyNotFoundException($"Payment {paymentId} not found");

            // 3. Parse items
            var items = new List<PaymentItem>();
            try
            {
                if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                {
                    items = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize items for payment {PaymentId}", paymentId);
            }

            // 4. Signature: Payment.TseSignature = COMPACT JWS. PrevSignatureValue = imza zinciri (stateful)
            var signatureValue = payment.TseSignature ?? string.Empty;
            var prevSignatureValue = payment.PrevSignatureValueUsed
                ?? await GetLastSignatureValueForKassenIdAsync(payment.KassenId ?? string.Empty);

            var certInfo = await _tseService.GetTseCertificateInfoAsync(payment.KassenId ?? string.Empty);
            var qrPayload = string.IsNullOrEmpty(signatureValue) ? string.Empty : $"_R1-AT1_{payment.KassenId}_{payment.ReceiptNumber}_{payment.CreatedAt:s}_{payment.TotalAmount:0.00}_0.00_{certInfo.CertificateNumber}_{signatureValue}";

            // 5. Create Entity
            var newReceipt = new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = payment.Id,
                ReceiptNumber = payment.ReceiptNumber ?? $"TEMP-{payment.Id.ToString()[..8]}",
                IssuedAt = payment.CreatedAt,
                CashierId = payment.CashierId,
                CashRegisterId = Guid.TryParse(payment.KassenId, out var parsedKassenId) ? parsedKassenId : Guid.Empty,
                SubTotal = payment.TotalAmount - payment.TaxAmount,
                TaxTotal = payment.TaxAmount,
                GrandTotal = payment.TotalAmount,
                QrCodePayload = qrPayload,
                SignatureValue = signatureValue,
                PrevSignatureValue = prevSignatureValue,
                CreatedAt = DateTime.UtcNow
            };

            // 6. Items: ana ürün satırı + her modifier için alt satır (ParentItemId ile; RKSV satış kalemi olarak sayılır)
            var receiptItems = new List<ReceiptItem>();
            var taxLineInputs = new List<(int TaxType, decimal TaxRate, decimal LineNet, decimal LineTax, decimal LineGross)>();
            foreach (var i in items)
            {
                var productItemId = Guid.NewGuid();
                receiptItems.Add(new ReceiptItem
                {
                    ItemId = productItemId,
                    ReceiptId = newReceipt.ReceiptId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    TaxRate = i.TaxRate * 100, // 0.20 -> 20.00
                    ParentItemId = null
                });
                taxLineInputs.Add((i.TaxType, i.TaxRate, i.LineNet, i.TaxAmount, i.TotalPrice));
                if (i.Modifiers != null)
                {
                    foreach (var m in i.Modifiers)
                    {
                        receiptItems.Add(new ReceiptItem
                        {
                            ItemId = Guid.NewGuid(),
                            ReceiptId = newReceipt.ReceiptId,
                            ProductName = m.Name,
                            Quantity = i.Quantity,
                            UnitPrice = m.UnitPrice,
                            TotalPrice = m.TotalPrice,
                            TaxRate = m.TaxRate * 100,
                            ParentItemId = productItemId
                        });
                        taxLineInputs.Add((m.TaxType, m.TaxRate, m.LineNet, m.TaxAmount, m.TotalPrice));
                    }
                }
            }
            newReceipt.Items = receiptItems;

            // 7. Tax Lines: tüm satırlardan (ürün + modifier) vergi grubu; RKSV uyumu
            var taxGroups = taxLineInputs
                .GroupBy(x => new { x.TaxType, x.TaxRate })
                .Select(g => new ReceiptTaxLine
                {
                    LineId = Guid.NewGuid(),
                    TaxType = g.Key.TaxType,
                    TaxRate = g.Key.TaxRate * 100,
                    NetAmount = g.Sum(x => x.LineNet),
                    TaxAmount = g.Sum(x => x.LineTax),
                    GrossAmount = g.Sum(x => x.LineGross)
                })
                .OrderBy(t => t.TaxRate)
                .ThenBy(t => t.TaxType)
                .ToList();

            newReceipt.TaxLines = taxGroups;

            // 8. Save
            _context.Receipts.Add(newReceipt);
            await _context.SaveChangesAsync();

            // Reload to get Payment navigation populated (or just set it manually for DTO mapping)
            newReceipt.Payment = payment;

            var signatureDto = new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = certInfo.CertificateNumber,
                Timestamp = payment.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = prevSignatureValue,
                SignatureValue = signatureValue,
                QrData = qrPayload
            };
            return MapToDTO(newReceipt, signatureDto);
        }

        private async Task<string> GetLastSignatureValueForKassenIdAsync(string kassenId)
        {
            if (string.IsNullOrEmpty(kassenId)) return string.Empty;
            var last = await _context.Receipts
                .Include(r => r.Payment)
                .Where(r => r.Payment != null && r.Payment.KassenId == kassenId && r.SignatureValue != null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            return last?.SignatureValue ?? string.Empty;
        }

        private ReceiptDTO MapToDTO(Receipt receipt, ReceiptSignatureDTO? explicitSig = null)
        {
            var header = new ReceiptHeaderDTO
            {
                ShopName = _companyProfile.CompanyName,
                Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}"
            };

            var company = new ReceiptCompanyDTO
            {
                Name = _companyProfile.CompanyName,
                Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}",
                TaxNumber = receipt.Payment?.Steuernummer ?? _companyProfile.TaxNumber
            };

            var signature = explicitSig ?? new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = "",
                Timestamp = receipt.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = receipt.PrevSignatureValue ?? "",
                SignatureValue = receipt.SignatureValue ?? "",
                QrData = receipt.QrCodePayload ?? ""
            };

            return new ReceiptDTO
            {
                ReceiptId = receipt.ReceiptId,
                ReceiptNumber = receipt.ReceiptNumber,
                Date = receipt.IssuedAt,
                CashierName = receipt.CashierId ?? "Unknown", // Resolve name if needed
                KassenID = receipt.CashRegisterId.ToString(),
                TableNumber = receipt.Payment?.TableNumber,
                
                Company = company,
                Header = header,
                
                Items = receipt.Items.Select(i => new ReceiptItemDTO
                {
                    ItemId = i.ItemId,
                    Name = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    TaxRate = i.TaxRate,
                    ParentItemId = i.ParentItemId,
                    IsModifierLine = i.ParentItemId != null
                }).ToList(),
                
                SubTotal = receipt.SubTotal,
                TaxAmount = receipt.TaxTotal,
                GrandTotal = receipt.GrandTotal,
                
                TaxRates = receipt.TaxLines.Select(t => new ReceiptTaxLineDTO
                {
                    TaxType = t.TaxType,
                    Rate = t.TaxRate,
                    GrossAmount = t.GrossAmount,
                    TaxAmount = t.TaxAmount,
                    NetAmount = t.NetAmount
                }).ToList(),
                
                Payments = new List<ReceiptPaymentDTO>
                {
                    new ReceiptPaymentDTO
                    {
                        Method = receipt.Payment?.PaymentMethod.ToString() ?? "Cash",
                        Amount = receipt.GrandTotal,
                        Tendered = receipt.GrandTotal,
                        Change = 0
                    }
                },
                
                Signature = signature,
                FooterText = _companyProfile.FooterText
            };
        }

    }
}
