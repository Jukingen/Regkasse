using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs.Rksv;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Rksv;

public interface IJahresbelegService
{
    Task<JahresbelegResult> CreateJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        bool useDecemberMonatsbeleg = false,
        CancellationToken cancellationToken = default);

    Task<JahresbelegResult> GetJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default);

    Task<List<JahresbelegSummary>> GetJahresbelegHistoryAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<bool> JahresbelegExistsAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default);

    Task<JahresbelegResult> CreateFromDecemberMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default);
}

public sealed class JahresbelegService : IJahresbelegService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly ITseService _tseService;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly IMonatsbelegService _monatsbelegService;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<JahresbelegService> _logger;

    public JahresbelegService(
        AppDbContext db,
        ITseService tseService,
        ITseKeyProvider tseKeyProvider,
        IMonatsbelegService monatsbelegService,
        IRksvEnvironmentService rksvEnv,
        ICurrentUserService currentUserService,
        ILogger<JahresbelegService> logger)
    {
        _db = db;
        _tseService = tseService;
        _tseKeyProvider = tseKeyProvider;
        _monatsbelegService = monatsbelegService;
        _rksvEnv = rksvEnv;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<JahresbelegResult> CreateJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        bool useDecemberMonatsbeleg = false,
        CancellationToken cancellationToken = default)
    {
        if (await JahresbelegExistsAsync(cashRegisterId, year, cancellationToken))
        {
            throw new InvalidOperationException($"Jahresbeleg for {year} already exists");
        }

        if (useDecemberMonatsbeleg)
        {
            return await CreateFromDecemberMonatsbelegAsync(cashRegisterId, year, cancellationToken);
        }

        var register = await RequireRegisterAsync(cashRegisterId, cancellationToken);
        var monthlyClosings = new List<MonatsbelegResult>();

        for (var month = 1; month <= 12; month++)
        {
            try
            {
                var monatsbeleg = await _monatsbelegService.GetMonatsbelegAsync(
                    cashRegisterId,
                    year,
                    month,
                    cancellationToken);
                monthlyClosings.Add(monatsbeleg);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Monatsbeleg for {Year}-{Month:00} not found", year, month);
            }
        }

        if (monthlyClosings.Count == 0)
        {
            throw new InvalidOperationException($"No Monatsbeleg found for {year}");
        }

        var summary = CalculateYearlySummary(monthlyClosings);
        var previousJahresbeleg = await _db.Jahresbelege.AsNoTracking()
            .Where(j => j.CashRegisterId == cashRegisterId && j.Year < year)
            .OrderByDescending(j => j.Year)
            .FirstOrDefaultAsync(cancellationToken);

        var previousSignature = previousJahresbeleg?.TseSignature;
        var chainLength = (previousJahresbeleg?.SignatureChainLength ?? 0) + 1;
        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var signedAtUtc = DateTime.UtcNow;
        var yearAnchor = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var tseSignature = await _tseService.CreateYearlyClosingSignatureAsync(
            cashRegisterId,
            register.RegisterNumber,
            yearAnchor,
            summary.TotalGross,
            summary.TransactionCount);

        var monthlyRefs = monthlyClosings
            .Select(m => new JahresbelegMonthlyReferenceDto { Year = m.Year, Month = m.Month, Id = m.Id })
            .ToList();

        var jahresbeleg = BuildEntity(
            cashRegisterId,
            year,
            summary,
            JahresbelegYearlyAggregator.SerializeMonthlyReferences(monthlyRefs),
            tseSignature,
            signedAtUtc.ToString("O"),
            _tseKeyProvider.GetCurrentCertificateThumbprint(),
            previousSignature,
            chainLength,
            isDemo,
            isDecemberMonatsbeleg: false);

        _db.Jahresbelege.Add(jahresbeleg);
        register.LastJahresbelegUtc = signedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Jahresbeleg created for {Year}, register {RegisterId}, simulated: {IsSimulated}",
            year,
            cashRegisterId,
            isDemo);

        return await MapToResultAsync(jahresbeleg, cancellationToken);
    }

    public async Task<JahresbelegResult> CreateFromDecemberMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (await JahresbelegExistsAsync(cashRegisterId, year, cancellationToken))
        {
            throw new InvalidOperationException($"Jahresbeleg for {year} already exists");
        }

        var decemberMonatsbeleg = await _monatsbelegService.GetMonatsbelegAsync(
            cashRegisterId,
            year,
            12,
            cancellationToken);

        var register = await RequireRegisterAsync(cashRegisterId, cancellationToken);
        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var signedAtUtc = DateTime.UtcNow;

        var monthlyRefs = JahresbelegYearlyAggregator.SerializeMonthlyReferences(
            new[]
            {
                new JahresbelegMonthlyReferenceDto
                {
                    Year = year,
                    Month = 12,
                    Id = decemberMonatsbeleg.Id,
                },
            });

        var summary = new YearlySummary
        {
            TotalCash = decemberMonatsbeleg.TotalCash,
            TotalCard = decemberMonatsbeleg.TotalCard,
            TotalVoucher = decemberMonatsbeleg.TotalVoucher,
            TotalOther = decemberMonatsbeleg.TotalOther,
            TotalGross = decemberMonatsbeleg.TotalGross,
            TotalTax = decemberMonatsbeleg.TotalTax,
            TaxRate20 = decemberMonatsbeleg.TaxRate20,
            TaxRate10 = decemberMonatsbeleg.TaxRate10,
            TaxRate0 = decemberMonatsbeleg.TaxRate0,
            TransactionCount = decemberMonatsbeleg.TransactionCount,
        };

        var jahresbeleg = BuildEntity(
            cashRegisterId,
            year,
            summary,
            monthlyRefs,
            decemberMonatsbeleg.TseSignature,
            decemberMonatsbeleg.TseSignatureTimestamp,
            decemberMonatsbeleg.TseCertificateThumbprint,
            decemberMonatsbeleg.PreviousSignature,
            decemberMonatsbeleg.SignatureChainLength,
            isDemo,
            isDecemberMonatsbeleg: true);

        _db.Jahresbelege.Add(jahresbeleg);
        register.LastJahresbelegUtc = signedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Jahresbeleg created from December Monatsbeleg for {Year}, register {RegisterId}",
            year,
            cashRegisterId);

        return await MapToResultAsync(jahresbeleg, cancellationToken);
    }

    public async Task<JahresbelegResult> GetJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var jahresbeleg = await _db.Jahresbelege.AsNoTracking()
            .Include(j => j.CashRegister)
            .FirstOrDefaultAsync(
                j => j.CashRegisterId == cashRegisterId && j.Year == year,
                cancellationToken);

        if (jahresbeleg == null)
        {
            throw new KeyNotFoundException($"Jahresbeleg for {year} not found");
        }

        return MapToResult(jahresbeleg);
    }

    public async Task<List<JahresbelegSummary>> GetJahresbelegHistoryAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default) =>
        await _db.Jahresbelege.AsNoTracking()
            .Where(j => j.CashRegisterId == cashRegisterId)
            .OrderByDescending(j => j.Year)
            .Select(j => new JahresbelegSummary
            {
                Year = j.Year,
                TotalGross = j.TotalGross,
                TotalTax = j.TotalTax,
                TransactionCount = j.TransactionCount,
                CreatedAt = j.CreatedAtUtc,
                IsSimulated = j.IsSimulated,
                HasSignature = j.TseSignature != null && j.TseSignature != string.Empty,
                IsDecemberMonatsbeleg = j.IsDecemberMonatsbeleg,
            })
            .ToListAsync(cancellationToken);

    public Task<bool> JahresbelegExistsAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default) =>
        _db.Jahresbelege.AsNoTracking()
            .AnyAsync(j => j.CashRegisterId == cashRegisterId && j.Year == year, cancellationToken);

    private Jahresbeleg BuildEntity(
        Guid cashRegisterId,
        int year,
        YearlySummary summary,
        string monthlyReferencesJson,
        string? tseSignature,
        string? tseSignatureTimestamp,
        string? certificateThumbprint,
        string? previousSignature,
        int chainLength,
        bool isDemo,
        bool isDecemberMonatsbeleg)
    {
        var actorId = _currentUserService.GetCurrentUserId();
        var actorUserId = actorId == Guid.Empty ? "system" : actorId.ToString();
        var now = DateTime.UtcNow;

        return new Jahresbeleg
        {
            CashRegisterId = cashRegisterId,
            Year = year,
            TotalCash = summary.TotalCash,
            TotalCard = summary.TotalCard,
            TotalVoucher = summary.TotalVoucher,
            TotalOther = summary.TotalOther,
            TotalGross = summary.TotalGross,
            TotalTax = summary.TotalTax,
            TaxRate20 = summary.TaxRate20,
            TaxRate10 = summary.TaxRate10,
            TaxRate0 = summary.TaxRate0,
            TransactionCount = summary.TransactionCount,
            MonthlyReferences = monthlyReferencesJson,
            TseSignature = tseSignature,
            TseSignatureTimestamp = tseSignatureTimestamp,
            TseCertificateThumbprint = certificateThumbprint,
            PreviousSignature = previousSignature,
            SignatureChainLength = chainLength,
            IsSimulated = isDemo,
            Environment = isDemo ? "Demo" : "Production",
            IsDecemberMonatsbeleg = isDecemberMonatsbeleg,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    private async Task<CashRegister> RequireRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var register = await _db.CashRegisters
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken);

        if (register == null || register.Status == RegisterStatus.Decommissioned)
        {
            throw new InvalidOperationException($"Cash register {cashRegisterId} is not available.");
        }

        return register;
    }

    private async Task<JahresbelegResult> MapToResultAsync(
        Jahresbeleg entity,
        CancellationToken cancellationToken)
    {
        if (entity.CashRegister != null)
        {
            return MapToResult(entity);
        }

        await _db.Entry(entity).Reference(j => j.CashRegister).LoadAsync(cancellationToken);
        return MapToResult(entity);
    }

    private JahresbelegResult MapToResult(Jahresbeleg j) =>
        new()
        {
            Id = j.Id,
            CashRegisterId = j.CashRegisterId,
            CashRegisterName = j.CashRegister?.RegisterNumber ?? "Unknown",
            Year = j.Year,
            CreatedAt = j.CreatedAtUtc,
            TotalCash = j.TotalCash,
            TotalCard = j.TotalCard,
            TotalVoucher = j.TotalVoucher,
            TotalOther = j.TotalOther,
            TotalGross = j.TotalGross,
            TotalTax = j.TotalTax,
            TaxRate20 = j.TaxRate20,
            TaxRate10 = j.TaxRate10,
            TaxRate0 = j.TaxRate0,
            TransactionCount = j.TransactionCount,
            MonthlyReferences = j.MonthlyReferences,
            TseSignature = j.TseSignature,
            TseSignatureTimestamp = j.TseSignatureTimestamp,
            PreviousSignature = j.PreviousSignature,
            SignatureChainLength = j.SignatureChainLength,
            IsSimulated = j.IsSimulated,
            Environment = j.Environment,
            IsDecemberMonatsbeleg = j.IsDecemberMonatsbeleg,
            TseStatusDisplay = _rksvEnv.GetTseStatusDisplay(),
        };

    private static YearlySummary CalculateYearlySummary(IEnumerable<MonatsbelegResult> monthlies) =>
        new()
        {
            TotalCash = monthlies.Sum(m => m.TotalCash),
            TotalCard = monthlies.Sum(m => m.TotalCard),
            TotalVoucher = monthlies.Sum(m => m.TotalVoucher),
            TotalOther = monthlies.Sum(m => m.TotalOther),
            TotalGross = monthlies.Sum(m => m.TotalGross),
            TotalTax = monthlies.Sum(m => m.TotalTax),
            TaxRate20 = monthlies.Sum(m => m.TaxRate20),
            TaxRate10 = monthlies.Sum(m => m.TaxRate10),
            TaxRate0 = monthlies.Sum(m => m.TaxRate0),
            TransactionCount = monthlies.Sum(m => m.TransactionCount),
        };
}
