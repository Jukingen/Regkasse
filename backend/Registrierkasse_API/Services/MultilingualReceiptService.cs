using System.Text;
using System.Text.Json;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// Çok dilli fiş ve PDF servisi - Avusturya yasal zorunluluklarına uygun
    /// </summary>
    public class MultilingualReceiptService
    {
        private readonly ILogger<MultilingualReceiptService> _logger;

        public MultilingualReceiptService(ILogger<MultilingualReceiptService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Çok dilli fiş içeriği oluşturur - Almanca, İngilizce ve Türkçe
        /// </summary>
        public string GenerateMultilingualReceipt(Receipt receipt, string language = "de-DE")
        {
            try
            {
                var template = GetReceiptTemplate(language);
                var items = receipt.Items != null ? receipt.Items.ToList() : new List<Registrierkasse_API.Models.ReceiptItem>();
                
                var receiptContent = new StringBuilder();
                
                // Başlık
                receiptContent.AppendLine(template.Title);
                receiptContent.AppendLine(new string('=', 40));
                receiptContent.AppendLine();
                
                // Firma bilgileri
                receiptContent.AppendLine(template.CompanyName);
                receiptContent.AppendLine(template.CompanyAddress);
                receiptContent.AppendLine(template.CompanyTaxNumber);
                receiptContent.AppendLine();
                
                // Fiş detayları
                receiptContent.AppendLine($"{template.ReceiptNumber}: {receipt.ReceiptNumber}");
                receiptContent.AppendLine($"{template.Date}: {receipt.ReceiptDate:dd.MM.yyyy}");
                receiptContent.AppendLine($"{template.Time}: {receipt.ReceiptDate:HH:mm:ss}");
                receiptContent.AppendLine($"{template.CashRegisterId}: {receipt.KassenId ?? "N/A"}");
                receiptContent.AppendLine();
                
                // TSE imzası
                if (!string.IsNullOrEmpty(receipt.TseSignature))
                {
                    receiptContent.AppendLine($"{template.TseSignature}: {receipt.TseSignature}");
                    receiptContent.AppendLine();
                }
                
                // Ürün listesi
                receiptContent.AppendLine(template.ItemsHeader);
                receiptContent.AppendLine(new string('-', 40));
                
                foreach (var item in items)
                {
                    receiptContent.AppendLine($"{item.Product?.Name ?? "Unknown Product"}");
                    receiptContent.AppendLine($"  {template.Quantity}: {item.Quantity} x {item.UnitPrice:C} = {item.Quantity * item.UnitPrice:C}");
                }
                
                receiptContent.AppendLine(new string('-', 40));
                
                // Toplamlar
                receiptContent.AppendLine($"{template.Subtotal}: {receipt.Subtotal:C}");
                receiptContent.AppendLine($"{template.TaxAmount}: {receipt.TaxAmount:C}");
                receiptContent.AppendLine($"{template.TotalAmount}: {receipt.TotalAmount:C}");
                receiptContent.AppendLine();
                
                // Ödeme yöntemi
                receiptContent.AppendLine($"{template.PaymentMethod}: {GetPaymentMethodText(receipt.PaymentMethod, language)}");
                receiptContent.AppendLine();
                
                // Yasal uyarılar
                receiptContent.AppendLine(template.LegalNotice);
                receiptContent.AppendLine(template.SignatureRequired);
                receiptContent.AppendLine();
                
                // QR kod bilgisi (varsa)
                if (!string.IsNullOrEmpty(receipt.TseSignature))
                {
                    receiptContent.AppendLine(template.QrCodeInfo);
                    receiptContent.AppendLine(receipt.TseSignature);
                    receiptContent.AppendLine();
                }
                
                // Teşekkür mesajı
                receiptContent.AppendLine(template.ThankYou);
                receiptContent.AppendLine();
                
                // Alt bilgi
                receiptContent.AppendLine(new string('=', 40));
                receiptContent.AppendLine(template.Footer);
                
                _logger.LogInformation($"Multilingual receipt generated for receipt {receipt.ReceiptNumber} in {language}");
                
                return receiptContent.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating multilingual receipt for {receipt.ReceiptNumber}");
                throw;
            }
        }

        /// <summary>
        /// Çok dilli PDF içeriği oluşturur
        /// </summary>
        public byte[] GenerateMultilingualPdf(Receipt receipt, string language = "de-DE")
        {
            try
            {
                var receiptText = GenerateMultilingualReceipt(receipt, language);
                
                // PDF oluşturma işlemi burada yapılacak
                // Gerçek implementasyonda iTextSharp veya başka bir PDF kütüphanesi kullanılır
                
                var pdfBytes = Encoding.UTF8.GetBytes(receiptText); // Geçici olarak text'i byte'a çeviriyoruz
                
                _logger.LogInformation($"Multilingual PDF generated for receipt {receipt.ReceiptNumber} in {language}");
                
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating multilingual PDF for {receipt.ReceiptNumber}");
                throw;
            }
        }

        /// <summary>
        /// Dil bazlı fiş template'i döndürür
        /// </summary>
        private ReceiptTemplate GetReceiptTemplate(string language)
        {
            return language switch
            {
                "de-DE" => new ReceiptTemplate
                {
                    Title = "KASSENBON",
                    CompanyName = "Musterfirma GmbH",
                    CompanyAddress = "Hauptstraße 123, 1010 Wien",
                    CompanyTaxNumber = "UID: ATU12345678",
                    ReceiptNumber = "Beleg-Nr.",
                    Date = "Datum",
                    Time = "Uhrzeit",
                    CashRegisterId = "Kassen-ID",
                    TseSignature = "TSE-Signatur",
                    ItemsHeader = "Artikel",
                    Quantity = "Menge",
                    Subtotal = "Zwischensumme",
                    TaxAmount = "Steuerbetrag",
                    TotalAmount = "Gesamtbetrag",
                    PaymentMethod = "Zahlungsart",
                    LegalNotice = "Dieser Beleg ist steuerrechtlich relevant.",
                    SignatureRequired = "Unterschrift erforderlich",
                    QrCodeInfo = "QR-Code für digitale Überprüfung:",
                    ThankYou = "Vielen Dank für Ihren Einkauf!",
                    Footer = "Für Fragen wenden Sie sich an unseren Kundenservice."
                },
                
                "en" => new ReceiptTemplate
                {
                    Title = "RECEIPT",
                    CompanyName = "Sample Company Ltd.",
                    CompanyAddress = "Main Street 123, 1010 Vienna",
                    CompanyTaxNumber = "VAT: ATU12345678",
                    ReceiptNumber = "Receipt No.",
                    Date = "Date",
                    Time = "Time",
                    CashRegisterId = "Cash Register ID",
                    TseSignature = "TSE Signature",
                    ItemsHeader = "Items",
                    Quantity = "Qty",
                    Subtotal = "Subtotal",
                    TaxAmount = "Tax Amount",
                    TotalAmount = "Total Amount",
                    PaymentMethod = "Payment Method",
                    LegalNotice = "This receipt is tax-relevant.",
                    SignatureRequired = "Signature required",
                    QrCodeInfo = "QR Code for digital verification:",
                    ThankYou = "Thank you for your purchase!",
                    Footer = "For questions, please contact our customer service."
                },
                
                "tr" => new ReceiptTemplate
                {
                    Title = "FİŞ",
                    CompanyName = "Örnek Şirket Ltd.",
                    CompanyAddress = "Ana Cadde 123, 1010 Viyana",
                    CompanyTaxNumber = "KDV: ATU12345678",
                    ReceiptNumber = "Fiş No.",
                    Date = "Tarih",
                    Time = "Saat",
                    CashRegisterId = "Kasa ID",
                    TseSignature = "TSE İmzası",
                    ItemsHeader = "Ürünler",
                    Quantity = "Miktar",
                    Subtotal = "Ara Toplam",
                    TaxAmount = "Vergi Tutarı",
                    TotalAmount = "Toplam Tutar",
                    PaymentMethod = "Ödeme Yöntemi",
                    LegalNotice = "Bu fiş vergi açısından önemlidir.",
                    SignatureRequired = "İmza gerekli",
                    QrCodeInfo = "Dijital doğrulama için QR kod:",
                    ThankYou = "Alışverişiniz için teşekkürler!",
                    Footer = "Sorularınız için müşteri hizmetlerimizle iletişime geçin."
                },
                
                _ => GetReceiptTemplate("de-DE") // Varsayılan olarak Almanca
            };
        }

        /// <summary>
        /// Ödeme yöntemi metnini dile göre döndürür
        /// </summary>
        private string GetPaymentMethodText(string paymentMethod, string language)
        {
            return paymentMethod.ToLower() switch
            {
                "cash" => language switch
                {
                    "de-DE" => "Bargeld",
                    "en" => "Cash",
                    "tr" => "Nakit",
                    _ => "Cash"
                },
                
                "card" => language switch
                {
                    "de-DE" => "Karte",
                    "en" => "Card",
                    "tr" => "Kart",
                    _ => "Card"
                },
                
                "voucher" => language switch
                {
                    "de-DE" => "Gutschein",
                    "en" => "Voucher",
                    "tr" => "Kupon",
                    _ => "Voucher"
                },
                
                _ => paymentMethod
            };
        }

        /// <summary>
        /// Tüm dillerde fiş oluşturur (denetim için)
        /// </summary>
        public Dictionary<string, string> GenerateAllLanguageReceipts(Receipt receipt)
        {
            var allReceipts = new Dictionary<string, string>();
            
            var languages = new[] { "de-DE", "en", "tr" };
            
            foreach (var language in languages)
            {
                allReceipts[language] = GenerateMultilingualReceipt(receipt, language);
            }
            
            return allReceipts;
        }
    }

    /// <summary>
    /// Fiş template modeli
    /// </summary>
    public class ReceiptTemplate
    {
        public string Title { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyTaxNumber { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string CashRegisterId { get; set; } = string.Empty;
        public string TseSignature { get; set; } = string.Empty;
        public string ItemsHeader { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string Subtotal { get; set; } = string.Empty;
        public string TaxAmount { get; set; } = string.Empty;
        public string TotalAmount { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string LegalNotice { get; set; } = string.Empty;
        public string SignatureRequired { get; set; } = string.Empty;
        public string QrCodeInfo { get; set; } = string.Empty;
        public string ThankYou { get; set; } = string.Empty;
        public string Footer { get; set; } = string.Empty;
    }
} 