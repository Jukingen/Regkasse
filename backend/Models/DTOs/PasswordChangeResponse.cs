namespace KasseAPI_Final.Models.DTOs;

public class PasswordChangeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? ErrorCodes { get; set; }
    public PasswordRequirements? Requirements { get; set; }
}

public class PasswordRequirements
{
    public int MinLength { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireNonAlphanumeric { get; set; }
}
