using System;
using System.Text;
using Registrierkasse.Models;
using System.Text.Json;

namespace Registrierkasse.Services
{
    public class InvoicePrintService
    {
        private const string LINE_SEPARATOR = "----------------------------------------";
        private const string DOUBLE_LINE = "========================================";
        private const int LINE_LENGTH = 40;
        private const string CURRENCY = "EUR";

        public string GenerateReceiptText(Invoice invoice)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("KASSA BELEG");
            sb.AppendLine(LINE_SEPARATOR);
            sb.AppendLine($"Beleg-Nr: {invoice.ReceiptNumber}");
            sb.AppendLine($"Datum: {invoice.CreatedAt:dd.MM.yyyy}");
            sb.AppendLine($"Uhrzeit: {invoice.CreatedAt:HH:mm:ss}");
            sb.AppendLine(LINE_SEPARATOR);

            // Customer Info
            if (invoice.CustomerDetails != null)
            {
                var customer = JsonSerializer.Deserialize<CustomerDetails>(invoice.CustomerDetails.RootElement.GetRawText());
                if (customer != null)
                {
                    if (!string.IsNullOrEmpty(customer.CompanyName))
                    {
                        sb.AppendLine($"Firma: {customer.CompanyName}");
                    }
                    if (!string.IsNullOrEmpty(customer.FirstName) || !string.IsNullOrEmpty(customer.LastName))
                    {
                        sb.AppendLine($"Name: {customer.FirstName} {customer.LastName}");
                    }
                    if (!string.IsNullOrEmpty(customer.TaxNumber))
                    {
                        sb.AppendLine($"Steuernummer: {customer.TaxNumber}");
                    }
                    if (!string.IsNullOrEmpty(customer.VatNumber))
                    {
                        sb.AppendLine($"USt-IdNr.: {customer.VatNumber}");
                    }
                }
                sb.AppendLine(LINE_SEPARATOR);
            }

            // Items
            sb.AppendLine("ARTIKEL");
            sb.AppendLine(LINE_SEPARATOR);
            foreach (var item in invoice.Items)
            {
                sb.AppendLine($"{item.Product.Name}");
                sb.AppendLine($"{item.Quantity,4} x {item.UnitPrice,8:F2} {CURRENCY}");
                if (item.DiscountAmount > 0)
                {
                    sb.AppendLine($"Rabatt: -{item.DiscountAmount,8:F2} {CURRENCY}");
                }
                sb.AppendLine($"MwSt. {GetTaxRateLabel(item.Product.TaxType)}: {item.TaxAmount,8:F2} {CURRENCY}");
                sb.AppendLine($"Gesamt: {item.TotalAmount,8:F2} {CURRENCY}");
                sb.AppendLine(LINE_SEPARATOR);
            }

            // Summary
            sb.AppendLine("ZUSAMMENFASSUNG");
            sb.AppendLine(LINE_SEPARATOR);
            var taxSummary = JsonSerializer.Deserialize<TaxSummary>(invoice.TaxSummary.RootElement.GetRawText());
            if (taxSummary != null)
            {
                if (taxSummary.StandardTaxBase > 0)
                {
                    sb.AppendLine($"20% MwSt. Basis: {taxSummary.StandardTaxBase,8:F2} {CURRENCY}");
                    sb.AppendLine($"20% MwSt. Betrag: {taxSummary.StandardTaxAmount,8:F2} {CURRENCY}");
                }
                if (taxSummary.ReducedTaxBase > 0)
                {
                    sb.AppendLine($"10% MwSt. Basis: {taxSummary.ReducedTaxBase,8:F2} {CURRENCY}");
                    sb.AppendLine($"10% MwSt. Betrag: {taxSummary.ReducedTaxAmount,8:F2} {CURRENCY}");
                }
                if (taxSummary.SpecialTaxBase > 0)
                {
                    sb.AppendLine($"13% MwSt. Basis: {taxSummary.SpecialTaxBase,8:F2} {CURRENCY}");
                    sb.AppendLine($"13% MwSt. Betrag: {taxSummary.SpecialTaxAmount,8:F2} {CURRENCY}");
                }
                if (taxSummary.ZeroTaxBase > 0)
                {
                    sb.AppendLine($"0% MwSt. Basis: {taxSummary.ZeroTaxBase,8:F2} {CURRENCY}");
                }
                if (taxSummary.ExemptTaxBase > 0)
                {
                    sb.AppendLine($"Steuerfrei Basis: {taxSummary.ExemptTaxBase,8:F2} {CURRENCY}");
                }
                sb.AppendLine(DOUBLE_LINE);
                sb.AppendLine($"Gesamtbetrag: {taxSummary.TotalAmount,8:F2} {CURRENCY}");
                sb.AppendLine($"MwSt. Gesamt: {taxSummary.TotalTaxAmount,8:F2} {CURRENCY}");
                sb.AppendLine(DOUBLE_LINE);
            }

            // Payment
            sb.AppendLine("ZAHLUNG");
            sb.AppendLine(LINE_SEPARATOR);
            var payment = JsonSerializer.Deserialize<PaymentDetails>(invoice.PaymentDetails.RootElement.GetRawText());
            if (payment != null)
            {
                sb.AppendLine($"Zahlungsart: {GetPaymentMethodLabel(payment.PaymentMethod)}");
                if (payment.CashAmount > 0)
                {
                    sb.AppendLine($"Bargeld: {payment.CashAmount,8:F2} {CURRENCY}");
                }
                if (payment.CardAmount > 0)
                {
                    sb.AppendLine($"Karte: {payment.CardAmount,8:F2} {CURRENCY}");
                    if (!string.IsNullOrEmpty(payment.CardType))
                    {
                        sb.AppendLine($"Kartentyp: {payment.CardType}");
                    }
                    if (!string.IsNullOrEmpty(payment.CardLastDigits))
                    {
                        sb.AppendLine($"Kartennummer: ****{payment.CardLastDigits}");
                    }
                }
                if (payment.VoucherAmount.HasValue && payment.VoucherAmount.Value > 0)
                {
                    sb.AppendLine($"Gutschein: {payment.VoucherAmount.Value,8:F2} {CURRENCY}");
                    if (!string.IsNullOrEmpty(payment.VoucherCode))
                    {
                        sb.AppendLine($"Gutscheincode: {payment.VoucherCode}");
                    }
                }
                if (payment.ChangeAmount > 0)
                {
                    sb.AppendLine($"Wechselgeld: {payment.ChangeAmount,8:F2} {CURRENCY}");
                }
            }
            sb.AppendLine(LINE_SEPARATOR);

            // TSE Info
            sb.AppendLine("TSE INFORMATIONEN");
            sb.AppendLine(LINE_SEPARATOR);
            sb.AppendLine($"TSE-Seriennummer: {invoice.TseSerialNumber}");
            sb.AppendLine($"TSE-Signatur: {invoice.TseSignature}");
            sb.AppendLine($"TSE-Zeitstempel: {invoice.TseTime:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"TSE-Prozessart: {invoice.TseProcessType}");
            sb.AppendLine(LINE_SEPARATOR);

            // Footer
            sb.AppendLine("Vielen Dank für Ihren Einkauf!");
            sb.AppendLine("Bitte bewahren Sie diesen Beleg auf.");
            sb.AppendLine(DOUBLE_LINE);

            return sb.ToString();
        }

        private string GetTaxRateLabel(TaxType taxType)
        {
            return taxType switch
            {
                TaxType.Standard => "20%",
                TaxType.Reduced => "10%",
                TaxType.Special => "13%",
                _ => taxType.ToString()
            };
        }

        private string GetPaymentMethodLabel(string paymentMethod)
        {
            return paymentMethod.ToLower() switch
            {
                "cash" => "Bar",
                "card" => "Karte",
                "voucher" => "Gutschein",
                "mixed" => "Gemischte Zahlung",
                _ => paymentMethod
            };
        }
    }
} 