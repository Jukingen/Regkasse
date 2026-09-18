namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Raised when EU reverse charge is calculated without a buyer VAT-ID that matches the
/// profile regex (AT / DE / CH prefix, otherwise the context profile).
/// </summary>
public sealed class VatIdShapeInvalidException : Exception
{
    public const string Code = "VAT_ID_SHAPE_INVALID";

    public VatIdShapeInvalidException()
        : base("Buyer VAT-ID shape is invalid.")
    {
    }

    public string ErrorCode => Code;
}
