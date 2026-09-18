using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly ICompanyProfileProvider _companyProfileProvider;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ICountryStrategyContext _countryStrategyContext;
    private readonly IInvoiceStrategyResolver _invoiceStrategyResolver;
    private readonly ITaxStrategyResolver _taxStrategyResolver;

    public InvoiceService(
        AppDbContext context,
        ICompanyProfileProvider companyProfileProvider,
        ISettingsTenantResolver settingsTenantResolver,
        ICountryStrategyContext? countryStrategyContext = null,
        IInvoiceStrategyResolver? invoiceStrategyResolver = null,
        ITaxStrategyResolver? taxStrategyResolver = null,
        ICountryProfileRegistry? countryProfileRegistry = null)
    {
        _context = context;
        _companyProfileProvider = companyProfileProvider;
        _settingsTenantResolver = settingsTenantResolver;
        var registry = countryProfileRegistry ?? new CountryProfileRegistry();
        _countryStrategyContext = countryStrategyContext
            ?? new CountryStrategyContext(_context, registry, _settingsTenantResolver);
        _invoiceStrategyResolver = invoiceStrategyResolver
            ?? CountryStrategyWiring.CreateInvoiceResolver();
        _taxStrategyResolver = taxStrategyResolver ?? CountryStrategyWiring.CreateTaxResolver();
    }

    public async Task<InvoiceDto> GenerateInvoiceAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await EnsureCountryInvoiceGateAsync(cancellationToken).ConfigureAwait(false);
        var invoice = await ResolveInvoiceFromPaymentCoreAsync(payment, cancellationToken).ConfigureAwait(false);
        return MapToDto(invoice);
    }

    public async Task<Invoice> ResolveInvoiceFromPaymentAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await EnsureCountryInvoiceGateAsync(cancellationToken).ConfigureAwait(false);
        return await ResolveInvoiceFromPaymentCoreAsync(payment, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureCountryInvoiceGateAsync(CancellationToken cancellationToken)
    {
        var binding = await _countryStrategyContext.LoadAsync(cancellationToken).ConfigureAwait(false);
        CountryCallSiteGuard.EnsureAustriaWired(binding.Profile, nameof(GenerateInvoiceAsync));
        var invoiceStrategy = _invoiceStrategyResolver.Resolve(binding.Profile, binding.VatRegime);
        _ = invoiceStrategy.GetMandatoryDisclosures(binding.Settings, customer: null);
        var taxStrategy = _taxStrategyResolver.Resolve(binding.Profile, binding.VatRegime);
        _ = taxStrategy.DetermineInvoiceFields(binding.Settings, customer: null);
    }

    private async Task<Invoice> ResolveInvoiceFromPaymentCoreAsync(PaymentDetails payment, CancellationToken cancellationToken)
    {
        var persisted = await _context.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.IsActive && (i.SourcePaymentId == payment.Id || i.Id == payment.Id),
                cancellationToken)
            .ConfigureAwait(false);

        if (persisted != null)
            return persisted;

        return await BuildInvoiceFromPaymentAsync(payment, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Invoice> BuildInvoiceFromPaymentAsync(PaymentDetails payment, CancellationToken cancellationToken)
    {
        var (sellerName, sellerAddress, sellerTaxNumber, sellerPhone, sellerEmail) =
            await ResolveSellerContextAsync(payment, cancellationToken).ConfigureAwait(false);

        var kassenId = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == payment.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        return new Invoice
        {
            Id = payment.Id,
            SourcePaymentId = payment.Id,
            InvoiceNumber = payment.ReceiptNumber ?? string.Empty,
            InvoiceDate = payment.CreatedAt,
            DueDate = payment.CreatedAt,
            Status = InvoiceStatus.Paid,
            Subtotal = payment.TotalAmount - payment.TaxAmount,
            TaxAmount = payment.TaxAmount,
            TotalAmount = payment.TotalAmount,
            PaidAmount = payment.TotalAmount,
            RemainingAmount = 0,
            CustomerName = payment.CustomerName,
            CustomerTaxNumber = payment.Steuernummer,
            CompanyName = sellerName,
            CompanyTaxNumber = sellerTaxNumber,
            CompanyAddress = sellerAddress,
            CompanyPhone = sellerPhone,
            CompanyEmail = sellerEmail,
            TseSignature = payment.TseSignature ?? string.Empty,
            KassenId = kassenId,
            CashRegisterId = payment.CashRegisterId,
            TseTimestamp = payment.TseTimestamp,
            PaymentMethod = payment.PaymentMethod,
            PaymentReference = payment.TransactionId,
            PaymentDate = payment.CreatedAt,
            InvoiceItems = payment.PaymentItems,
            TaxDetails = payment.TaxDetails ?? System.Text.Json.JsonDocument.Parse("{}"),
            IsActive = true,
            InvoiceDataProvenance = "DerivedFromPayment",
        };
    }

    private async Task<(string Name, string Address, string TaxNumber, string? Phone, string? Email)> ResolveSellerContextAsync(
        PaymentDetails payment,
        CancellationToken cancellationToken)
    {
        var liveProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var (name, address, taxNumber) = CompanyProfileMapper.ResolveForDisplay(payment, liveProfile);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await _context.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var phone = settings?.CompanyPhone;
        if (string.IsNullOrWhiteSpace(phone))
            phone = string.IsNullOrWhiteSpace(liveProfile.PhoneNumber) ? null : liveProfile.PhoneNumber;

        var email = settings?.CompanyEmail;
        if (string.IsNullOrWhiteSpace(email))
            email = string.IsNullOrWhiteSpace(liveProfile.Email) ? null : liveProfile.Email;

        return (name, address, taxNumber, phone, email);
    }

    private static InvoiceDto MapToDto(Invoice invoice) => new()
    {
        InvoiceId = invoice.Id,
        PaymentId = invoice.SourcePaymentId ?? invoice.Id,
        SellerName = invoice.CompanyName,
        SellerAddress = invoice.CompanyAddress,
        SellerTaxNumber = invoice.CompanyTaxNumber,
        SellerPhone = invoice.CompanyPhone,
        SellerEmail = invoice.CompanyEmail,
        InvoiceNumber = invoice.InvoiceNumber,
        InvoiceDate = invoice.InvoiceDate,
        DueDate = invoice.DueDate,
        CustomerName = invoice.CustomerName,
        CustomerTaxNumber = invoice.CustomerTaxNumber,
        Subtotal = invoice.Subtotal,
        TaxAmount = invoice.TaxAmount,
        TotalAmount = invoice.TotalAmount,
        PaidAmount = invoice.PaidAmount,
        TseSignature = invoice.TseSignature,
        KassenId = invoice.KassenId,
        CashRegisterId = invoice.CashRegisterId,
        TseTimestamp = invoice.TseTimestamp,
        PaymentMethod = invoice.PaymentMethod?.ToString(),
        InvoiceItems = invoice.InvoiceItems,
        TaxDetails = invoice.TaxDetails,
        DataProvenance = invoice.InvoiceDataProvenance,
    };
}
