using System;

namespace Registrierkasse_API.Models
{
    // Türkçe açıklama: DEP (DatenErfassungsProtokoll) kaydı modeli
    public class DepEntry
    {
        public Guid Id { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty; // JSON serialized
        public DateTime Timestamp { get; set; }
    }
} 