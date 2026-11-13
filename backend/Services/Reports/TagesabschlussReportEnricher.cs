using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

public interface ITagesabschlussReportEnricher
{
    Task<TagesabschlussCloudContext> BuildContextAsync(
        DailyClosing closing,
        CancellationToken cancellationToken = default);

    Task<TagesabschlussCloudContext> BuildContextForRegisterAsync(
        Guid cashRegisterId,
        DateTime? periodStartUtc = null,
        DateTime? periodEndUtc = null,
        bool isSimulated = false,
        string? tseSignature = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Loads tenant company profile, RKSV beleg status, and DEP export state for Cloud POS reports.</summary>
public sealed class TagesabschlussReportEnricher : ITagesabschlussReportEnricher
{
    private const string DepExportedLabel = "Exportiert";
    private const string DepPendingLabel = "Ausstehend";

    private readonly AppDbContext _db;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly IRksvMonatsbelegPolicy _monatsbelegPolicy;
    private readonly IRksvEnvironmentService _rksvEnvironment;

    public TagesabschlussReportEnricher(
        AppDbContext db,
        ICompanyProfileProvider companyProfile,
        IRksvMonatsbelegPolicy monatsbelegPolicy,
        IRksvEnvironmentService rksvEnvironment)
    {
        _db = db;
        _companyProfile = companyProfile;
        _monatsbelegPolicy = monatsbelegPolicy;
        _rksvEnvironment = rksvEnvironment;
    }

    public async Task<TagesabschlussCloudContext> BuildContextAsync(
        DailyClosing closing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(closing);

        var businessDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
        var (periodStartUtc, periodEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);

        return await BuildContextCoreAsync(
                closing.TenantId,
                closing.CashRegisterId,
                periodStartUtc,
                periodEndExclusiveUtc,
                closing.IsSimulated || _rksvEnvironment.IsTseSimulated(),
                closing.TseSignature,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<TagesabschlussCloudContext> BuildContextForRegisterAsync(
        Guid cashRegisterId,
        DateTime? periodStartUtc = null,
        DateTime? periodEndUtc = null,
        bool isSimulated = false,
        string? tseSignature = null,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));

        return BuildContextForRegisterResolvedAsync(
            cashRegisterId,
            periodStartUtc,
            periodEndUtc,
            isSimulated,
            tseSignature,
            cancellationToken);
    }

    private async Task<TagesabschlussCloudContext> BuildContextForRegisterResolvedAsync(
        Guid cashRegisterId,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc,
        bool isSimulated,
        string? tseSignature,
        CancellationToken cancellationToken)
    {
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        return await BuildContextCoreAsync(
                register.TenantId,
                cashRegisterId,
                periodStartUtc,
                periodEndUtc,
                isSimulated || _rksvEnvironment.IsTseSimulated(),
                tseSignature,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TagesabschlussCloudContext> BuildContextCoreAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc,
        bool isSimulated,
        string? tseSignature,
        CancellationToken cancellationToken)
    {
        var profile = await _companyProfile.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var companyAddress = CompanyProfileMapper.FormatCompanyAddress(
            profile.Street,
            profile.ZipCode,
            profile.City);

        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        var settings = await _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var decemberMbAsJahresbeleg = settings?.UseDecemberMonatsbelegAsJahresbeleg ?? true;

        var referenceDay = periodStartUtc.HasValue
            ? PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(periodStartUtc.Value)
            : PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

        var hasStartbeleg = register?.StartbelegCreatedAt.HasValue == true;
        var hasMonatsbeleg = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(
                cashRegisterId,
                referenceDay.Year,
                referenceDay.Month,
                cancellationToken)
            .ConfigureAwait(false);
        var hasJahresbeleg = await HasJahresbelegForViennaYearAsync(
                cashRegisterId,
                referenceDay.Year,
                decemberMbAsJahresbeleg,
                cancellationToken)
            .ConfigureAwait(false);

        var depExported = periodStartUtc.HasValue && periodEndUtc.HasValue
                          && await _db.DepExportHistories.AsNoTracking()
                              .AnyAsync(
                                  h => h.TenantId == tenantId
                                       && h.CashRegisterId == cashRegisterId
                                       && h.Status == DepExportStatus.Completed.ToString()
                                       && h.FromUtc <= periodStartUtc.Value
                                       && h.ToUtc >= periodEndUtc.Value,
                                  cancellationToken)
                              .ConfigureAwait(false);

        var tseProviderLabel = isSimulated
            ? "TSE simuliert (Demo)"
            : "fiskaly Cloud-HSM";

        var signatureVerified = !isSimulated && !string.IsNullOrWhiteSpace(tseSignature);

        return new TagesabschlussCloudContext
        {
            CompanyName = profile.CompanyName,
            CompanyAddress = companyAddress,
            CompanyVatId = profile.TaxNumber,
            RegisterNumber = register?.RegisterNumber,
            TseProviderLabel = tseProviderLabel,
            DepExportStatusLabel = depExported ? DepExportedLabel : DepPendingLabel,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            HasStartbeleg = hasStartbeleg,
            HasMonatsbeleg = hasMonatsbeleg,
            HasJahresbeleg = hasJahresbeleg,
            TseSignatureVerified = signatureVerified,
        };
    }

    private async Task<bool> HasJahresbelegForViennaYearAsync(
        Guid cashRegisterId,
        int year,
        bool decemberMonatsbelegCountsAsJahresbeleg,
        CancellationToken cancellationToken)
    {
        if (decemberMonatsbelegCountsAsJahresbeleg)
        {
            return await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == cashRegisterId
                         && p.IsActive
                         && (
                             (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                              && p.RksvSpecialReceiptYear == year)
                             || (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                                 && p.RksvSpecialReceiptYear == year
                                 && p.RksvSpecialReceiptMonth == 12)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == cashRegisterId
                     && p.IsActive
                     && p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                     && p.RksvSpecialReceiptYear == year,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
