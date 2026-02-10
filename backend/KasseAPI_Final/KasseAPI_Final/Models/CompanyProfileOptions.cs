using System;

namespace KasseAPI_Final.Models
{
    public class CompanyProfileOptions
    {
        public const string SectionName = "CompanyProfile";

        public string CompanyName { get; set; } = "Default Shop";
        public string TaxNumber { get; set; } = "ATU00000000";
        public string Street { get; set; } = "Default Street 1";
        public string ZipCode { get; set; } = "1010";
        public string City { get; set; } = "Wien";
        public string Country { get; set; } = "AT";
        public string PhoneNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";
        public string FooterText { get; set; } = "Thank you for your visit!";
        public string LogoUrl { get; set; } = "";
    }
}
