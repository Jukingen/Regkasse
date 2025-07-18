using System;

namespace Registrierkasse_API.Models
{
    public class Hardware : BaseEntity
    {
        public string Name { get; set; }
        public HardwareType Type { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string ConnectionType { get; set; }
        public string IPAddress { get; set; }
        public int? Port { get; set; }
        public string Status { get; set; }
        public string Location { get; set; }
        public string Configuration { get; set; }
        public DateTime? LastMaintenance { get; set; }
        public string Notes { get; set; }
    }

    public enum HardwareType
    {
        Printer,
        Scanner,
        CashDrawer,
        CardTerminal,
        Display,
        Scale,
        Other
    }
} 
