using System;
using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class InvoiceSendRequest
    {
        [Required]
        public Guid InvoiceId { get; set; }
        public string? SendTo { get; set; }
        public string? SentById { get; set; }
        public DateTime? SentDate { get; set; }
    }
} 