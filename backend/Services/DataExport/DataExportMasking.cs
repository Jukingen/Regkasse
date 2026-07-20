using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DataExport;

/// <summary>Masks RKSV cryptographic material for GDPR data-subject / mandant exports.</summary>
public static class DataExportMasking
{
    public const string Masked = "***";

    public static string MaskSecret(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : Masked;

    public static string? MaskOptionalSecret(string? value) =>
        string.IsNullOrEmpty(value) ? null : Masked;

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;
        var at = email.IndexOf('@');
        if (at <= 1)
            return Masked;
        return email[0] + "***" + email[at..];
    }

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return Masked;
        return "***" + digits[^4..];
    }

    public static object MapPayment(PaymentDetails p) => new
    {
        id = p.Id,
        customerId = p.CustomerId,
        customerName = p.CustomerName,
        tableNumber = p.TableNumber,
        cashierId = p.CashierId,
        totalAmount = p.TotalAmount,
        taxAmount = p.TaxAmount,
        paymentMethod = p.PaymentMethodRaw,
        steuernummer = p.Steuernummer,
        companyName = p.CompanyName,
        companyAddress = p.CompanyAddress,
        cashRegisterId = p.CashRegisterId,
        transactionId = p.TransactionId,
        tseSignature = MaskSecret(p.TseSignature),
        certificateThumbprint = MaskOptionalSecret(p.CertificateThumbprint),
        prevSignatureValueUsed = MaskOptionalSecret(p.PrevSignatureValueUsed),
        tseTimestamp = p.TseTimestamp,
        isRefund = p.IsRefund,
        isStorno = p.IsStorno,
        originalPaymentId = p.OriginalPaymentId,
        originalReceiptId = p.OriginalReceiptId,
        createdAt = p.CreatedAt,
        rksv = true,
        masked = true,
    };

    public static object MapReceipt(Receipt r) => new
    {
        receiptId = r.ReceiptId,
        paymentId = r.PaymentId,
        receiptNumber = r.ReceiptNumber,
        issuedAt = r.IssuedAt,
        cashierId = r.CashierId,
        cashRegisterId = r.CashRegisterId,
        subTotal = r.SubTotal,
        taxTotal = r.TaxTotal,
        grandTotal = r.GrandTotal,
        qrCodePayload = MaskOptionalSecret(r.QrCodePayload),
        signatureValue = MaskOptionalSecret(r.SignatureValue),
        prevSignatureValue = MaskOptionalSecret(r.PrevSignatureValue),
        signatureFormat = r.SignatureFormat,
        jwsHeader = MaskOptionalSecret(r.JwsHeader),
        jwsPayload = MaskOptionalSecret(r.JwsPayload),
        jwsSignature = MaskOptionalSecret(r.JwsSignature),
        provider = r.Provider,
        createdAt = r.CreatedAt,
        rksv = true,
        masked = true,
    };

    public static object MapInvoice(Invoice i) => new
    {
        id = i.Id,
        invoiceNumber = i.InvoiceNumber,
        invoiceDate = i.InvoiceDate,
        dueDate = i.DueDate,
        status = i.Status.ToString(),
        subtotal = i.Subtotal,
        taxAmount = i.TaxAmount,
        totalAmount = i.TotalAmount,
        paidAmount = i.PaidAmount,
        remainingAmount = i.RemainingAmount,
        customerName = i.CustomerName,
        customerEmail = MaskEmail(i.CustomerEmail),
        customerPhone = MaskPhone(i.CustomerPhone),
        customerAddress = i.CustomerAddress,
        customerTaxNumber = i.CustomerTaxNumber,
        companyName = i.CompanyName,
        companyTaxNumber = i.CompanyTaxNumber,
        companyAddress = i.CompanyAddress,
        cashRegisterId = i.CashRegisterId,
        kassenId = i.KassenId,
        tseSignature = MaskSecret(i.TseSignature),
        tseTimestamp = i.TseTimestamp,
        signatureFormat = i.SignatureFormat,
        jwsHeader = MaskOptionalSecret(i.JwsHeader),
        jwsPayload = MaskOptionalSecret(i.JwsPayload),
        jwsSignature = MaskOptionalSecret(i.JwsSignature),
        sourcePaymentId = i.SourcePaymentId,
        createdAt = i.CreatedAt,
        rksv = true,
        masked = true,
    };

    public static object MapProduct(Product p) => new
    {
        id = p.Id,
        name = p.Name,
        nameDe = p.NameDe,
        nameEn = p.NameEn,
        nameTr = p.NameTr,
        price = p.Price,
        categoryId = p.CategoryId,
        isActive = p.IsActive,
        createdAt = p.CreatedAt,
    };

    public static object MapCategory(Category c) => new
    {
        id = c.Id,
        key = c.Key,
        name = c.Name,
        description = c.Description,
        sortOrder = c.SortOrder,
        isActive = c.IsActive,
        createdAt = c.CreatedAt,
    };

    public static object MapCustomer(Customer c) => new
    {
        id = c.Id,
        name = c.Name,
        customerNumber = c.CustomerNumber,
        email = c.Email,
        phone = c.Phone,
        address = c.Address,
        taxNumber = c.TaxNumber,
        category = c.Category.ToString(),
        loyaltyPoints = c.LoyaltyPoints,
        totalSpent = c.TotalSpent,
        visitCount = c.VisitCount,
        lastVisit = c.LastVisit,
        isVip = c.IsVip,
        isSystem = c.IsSystem,
        createdAt = c.CreatedAt,
    };

    public static object MapOrder(OnlineOrder o, IEnumerable<OnlineOrderItem> items) => new
    {
        id = o.Id,
        orderNumber = o.OrderNumber,
        customerName = o.CustomerName,
        customerPhone = o.CustomerPhone,
        customerEmail = o.CustomerEmail,
        orderType = o.OrderType,
        tableNumber = o.TableNumber,
        deliveryAddress = o.DeliveryAddress,
        subtotal = o.Subtotal,
        tax = o.Tax,
        total = o.Total,
        paymentMethod = o.PaymentMethod,
        paymentStatus = o.PaymentStatus,
        orderStatus = o.OrderStatus,
        createdAt = o.CreatedAt,
        items = items.Select(i => new
        {
            id = i.Id,
            productId = i.ProductId,
            productName = i.ProductName,
            quantity = i.Quantity,
            unitPrice = i.Price,
            lineTotal = i.Total,
        }).ToList(),
    };

    public static object MapVoucher(Voucher v) => new
    {
        id = v.Id,
        customerId = v.CustomerId,
        maskedCode = v.MaskedCode,
        initialAmount = v.InitialAmount,
        remainingAmount = v.RemainingAmount,
        currency = v.Currency,
        status = v.Status.ToString(),
        createdAt = v.CreatedAtUtc,
    };

    public static object MapSettings(CompanySettings s) => new
    {
        companyName = s.CompanyName,
        companyAddress = s.CompanyAddress,
        companyPhone = s.CompanyPhone,
        companyEmail = s.CompanyEmail,
        companyWebsite = s.CompanyWebsite,
        companyTaxNumber = s.CompanyTaxNumber,
        updatedAt = s.UpdatedAt,
    };
}
