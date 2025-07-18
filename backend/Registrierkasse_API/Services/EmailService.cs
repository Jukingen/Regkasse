using System.Net.Mail;
using System.Net;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public interface IEmailService
    {
        Task<bool> SendInvoiceEmailAsync(Invoice invoice, string recipientEmail, byte[] pdfAttachment);
        Task<bool> SendInvoiceReminderAsync(Invoice invoice, string recipientEmail);
        Task<bool> SendPaymentConfirmationAsync(Invoice invoice, string recipientEmail);
        Task<bool> SendDailyReportEmailAsync(DailyReport report, string recipientEmail, byte[] pdfAttachment);
        Task<bool> TestEmailConnectionAsync();
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly SmtpClient _smtpClient;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // SMTP ayarları
            _smtpClient = new SmtpClient
            {
                Host = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com",
                Port = int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                EnableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true"),
                Credentials = new NetworkCredential(
                    _configuration["Email:Username"],
                    _configuration["Email:Password"]
                )
            };
        }

        public async Task<bool> SendInvoiceEmailAsync(Invoice invoice, string recipientEmail, byte[] pdfAttachment)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromAddress"] ?? "noreply@registrierkasse.at"),
                    Subject = $"Invoice #{invoice.InvoiceNumber} - {invoice.CompanyName}",
                    Body = GenerateInvoiceEmailBody(invoice),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                // PDF ekle
                if (pdfAttachment != null && pdfAttachment.Length > 0)
                {
                    var attachment = new Attachment(new MemoryStream(pdfAttachment), $"Invoice_{invoice.InvoiceNumber}.pdf", "application/pdf");
                    mailMessage.Attachments.Add(attachment);
                }

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Invoice email sent successfully to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice email to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);
                return false;
            }
        }

        public async Task<bool> SendInvoiceReminderAsync(Invoice invoice, string recipientEmail)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromAddress"] ?? "noreply@registrierkasse.at"),
                    Subject = $"Payment Reminder - Invoice #{invoice.InvoiceNumber}",
                    Body = GenerateReminderEmailBody(invoice),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Payment reminder sent to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment reminder to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);
                return false;
            }
        }

        public async Task<bool> SendPaymentConfirmationAsync(Invoice invoice, string recipientEmail)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromAddress"] ?? "noreply@registrierkasse.at"),
                    Subject = $"Payment Confirmation - Invoice #{invoice.InvoiceNumber}",
                    Body = GeneratePaymentConfirmationBody(invoice),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Payment confirmation sent to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment confirmation to {Email} for invoice {InvoiceNumber}", 
                    recipientEmail, invoice.InvoiceNumber);
                return false;
            }
        }

        public async Task<bool> SendDailyReportEmailAsync(DailyReport report, string recipientEmail, byte[] pdfAttachment)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromAddress"] ?? "noreply@registrierkasse.at"),
                    Subject = $"Daily Report - {report.ReportDate:dd.MM.yyyy}",
                    Body = GenerateDailyReportEmailBody(report),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                // PDF ekle
                if (pdfAttachment != null && pdfAttachment.Length > 0)
                {
                    var attachment = new Attachment(new MemoryStream(pdfAttachment), $"DailyReport_{report.ReportDate:yyyyMMdd}.pdf", "application/pdf");
                    mailMessage.Attachments.Add(attachment);
                }

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Daily report email sent to {Email} for date {Date}", 
                    recipientEmail, report.ReportDate);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send daily report email to {Email} for date {Date}", 
                    recipientEmail, report.ReportDate);
                return false;
            }
        }

        public async Task<bool> TestEmailConnectionAsync()
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromAddress"] ?? "noreply@registrierkasse.at"),
                    Subject = "Email Test - Registrierkasse",
                    Body = "This is a test email to verify email configuration.",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(_configuration["Email:TestRecipient"] ?? "test@example.com");

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email test failed");
                return false;
            }
        }

        private string GenerateInvoiceEmailBody(Invoice invoice)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .footer {{ background-color: #f5f5f5; padding: 20px; text-align: center; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #007AFF; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Invoice #{invoice.InvoiceNumber}</h1>
                        <p>{invoice.CompanyName}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Dear {invoice.CustomerName},</h2>
                        
                        <p>Please find attached the invoice for your recent purchase.</p>
                        
                        <h3>Invoice Details:</h3>
                        <ul>
                            <li><strong>Invoice Number:</strong> {invoice.InvoiceNumber}</li>
                            <li><strong>Invoice Date:</strong> {invoice.InvoiceDate:dd.MM.yyyy}</li>
                            <li><strong>Due Date:</strong> {invoice.DueDate:dd.MM.yyyy}</li>
                            <li><strong>Total Amount:</strong> <span class='amount'>€{invoice.TotalAmount:F2}</span></li>
                        </ul>
                        
                        <p>Payment is due within 30 days of the invoice date.</p>
                        
                        <h3>Payment Methods:</h3>
                        <ul>
                            <li>Bank Transfer</li>
                            <li>Credit Card</li>
                            <li>Cash</li>
                        </ul>
                        
                        <p>If you have any questions, please don't hesitate to contact us.</p>
                        
                        <p>Thank you for your business!</p>
                    </div>
                    
                    <div class='footer'>
                        <p>{invoice.CompanyName}</p>
                        <p>{invoice.CompanyAddress}</p>
                        <p>Phone: {invoice.CompanyPhone}</p>
                        <p>Email: {invoice.CompanyEmail}</p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateReminderEmailBody(Invoice invoice)
        {
            var daysOverdue = (DateTime.UtcNow - invoice.DueDate).Days;
            
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #FF6B35; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .footer {{ background-color: #f5f5f5; padding: 20px; text-align: center; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #FF6B35; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Payment Reminder</h1>
                        <p>Invoice #{invoice.InvoiceNumber}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Dear {invoice.CustomerName},</h2>
                        
                        <p>This is a friendly reminder that payment for the following invoice is overdue:</p>
                        
                        <h3>Invoice Details:</h3>
                        <ul>
                            <li><strong>Invoice Number:</strong> {invoice.InvoiceNumber}</li>
                            <li><strong>Invoice Date:</strong> {invoice.InvoiceDate:dd.MM.yyyy}</li>
                            <li><strong>Due Date:</strong> {invoice.DueDate:dd.MM.yyyy}</li>
                            <li><strong>Days Overdue:</strong> {daysOverdue}</li>
                            <li><strong>Amount Due:</strong> <span class='amount'>€{invoice.RemainingAmount:F2}</span></li>
                        </ul>
                        
                        <p>Please arrange payment as soon as possible to avoid any late fees.</p>
                        
                        <h3>Payment Methods:</h3>
                        <ul>
                            <li>Bank Transfer</li>
                            <li>Credit Card</li>
                            <li>Cash</li>
                        </ul>
                        
                        <p>If you have already made payment, please disregard this reminder.</p>
                        
                        <p>Thank you for your prompt attention to this matter.</p>
                    </div>
                    
                    <div class='footer'>
                        <p>{invoice.CompanyName}</p>
                        <p>{invoice.CompanyAddress}</p>
                        <p>Phone: {invoice.CompanyPhone}</p>
                        <p>Email: {invoice.CompanyEmail}</p>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePaymentConfirmationBody(Invoice invoice)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #10B981; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .footer {{ background-color: #f5f5f5; padding: 20px; text-align: center; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #10B981; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Payment Confirmation</h1>
                        <p>Invoice #{invoice.InvoiceNumber}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Dear {invoice.CustomerName},</h2>
                        
                        <p>Thank you! We have received your payment for the following invoice:</p>
                        
                        <h3>Payment Details:</h3>
                        <ul>
                            <li><strong>Invoice Number:</strong> {invoice.InvoiceNumber}</li>
                            <li><strong>Payment Date:</strong> {invoice.PaymentDate:dd.MM.yyyy}</li>
                            <li><strong>Payment Method:</strong> {invoice.PaymentMethod}</li>
                            <li><strong>Amount Paid:</strong> <span class='amount'>€{invoice.PaidAmount:F2}</span></li>
                            <li><strong>Remaining Balance:</strong> €{invoice.RemainingAmount:F2}</li>
                        </ul>
                        
                        <p>Your payment has been processed successfully.</p>
                        
                        <p>If you have any questions about this payment, please contact us.</p>
                        
                        <p>Thank you for your business!</p>
                    </div>
                    
                    <div class='footer'>
                        <p>{invoice.CompanyName}</p>
                        <p>{invoice.CompanyAddress}</p>
                        <p>Phone: {invoice.CompanyPhone}</p>
                        <p>Email: {invoice.CompanyEmail}</p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateDailyReportEmailBody(DailyReport report)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .footer {{ background-color: #f5f5f5; padding: 20px; text-align: center; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #007AFF; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Daily Report</h1>
                        <p>{report.ReportDate:dd.MM.yyyy}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Daily Sales Summary</h2>
                        
                        <h3>Financial Summary:</h3>
                        <ul>
                            <li><strong>Total Sales:</strong> <span class='amount'>€{report.TotalSales:F2}</span></li>
                            <li><strong>Total Transactions:</strong> {report.TotalTransactions}</li>
                            <li><strong>Cash Payments:</strong> €{report.CashPayments:F2}</li>
                            <li><strong>Card Payments:</strong> €{report.CardPayments:F2}</li>
                        </ul>
                        
                        <h3>TSE Information:</h3>
                        <ul>
                            <li><strong>Kassen ID:</strong> {report.KassenId}</li>
                            <li><strong>Report Time:</strong> {report.ReportTime:dd.MM.yyyy HH:mm:ss}</li>
                        </ul>
                        
                        <p>Please find the detailed report attached.</p>
                    </div>
                    
                    <div class='footer'>
                        <p>Registrierkasse GmbH</p>
                        <p>Automated Daily Report</p>
                    </div>
                </body>
                </html>";
        }
    }
} 
