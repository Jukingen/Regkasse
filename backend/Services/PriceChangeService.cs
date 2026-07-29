using System.Data;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Orchestrates catalog price/tax changes with append-only version + history rows (RKSV trail).
/// Products with prior sales history are superseded by a new catalog product (old row archived).
/// </summary>
public sealed class PriceChangeService : IPriceChangeService
{
    private readonly AppDbContext _db;
    private readonly IProductPriceHistoryService _priceHistoryService;
    private readonly IRksvPriceChangeComplianceChecker _complianceChecker;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PriceChangeService> _logger;

    public PriceChangeService(
        AppDbContext db,
        IProductPriceHistoryService priceHistoryService,
        IRksvPriceChangeComplianceChecker complianceChecker,
        IAuditLogService auditLogService,
        ILogger<PriceChangeService> logger)
    {
        _db = db;
        _priceHistoryService = priceHistoryService;
        _complianceChecker = complianceChecker;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PriceChangeResult> ChangePriceAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePriceChangeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
            return PriceChangeResult.Fail(validation.ErrorMessage ?? "Price change validation failed.");

        if (validation.RequiresNewProductVersion && !request.ForceInPlaceUpdate)
        {
            return await ChangePriceViaNewProductVersionAsync(request, validation, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ChangePriceInPlaceAsync(request, validation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Product> CreateNewProductVersionAsync(
        Guid productId,
        PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(request));
        if (request.NewTaxGroupId == Guid.Empty)
            throw new ArgumentException("New tax group id is required.", nameof(request));

        request.ProductId = productId;

        var original = await _db.Products
            .Include(p => p.ModifierGroupAssignments)
            .FirstOrDefaultAsync(
                p => p.Id == productId && p.TenantId == request.TenantId,
                cancellationToken)
            .ConfigureAwait(false);

        if (original is null)
            throw new KeyNotFoundException("Product not found");

        var newGroup = await _db.TaxGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Id == request.NewTaxGroupId
                     && g.TenantId == request.TenantId
                     && g.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        if (newGroup is null)
            throw new KeyNotFoundException("Tax group not found or inactive.");

        var now = DateTime.UtcNow;
        var newPrice = decimal.Round(request.NewPrice, 2, MidpointRounding.AwayFromZero);
        var newTaxRate = decimal.Round(newGroup.Rate, 2, MidpointRounding.AwayFromZero);
        var activeBarcode = original.Barcode;

        // Free the scannable barcode for the successor; keep archived barcode unique per tenant.
        original.Barcode = BuildArchivedBarcode(activeBarcode, original.Version, original.Id);
        original.IsActive = false;
        original.ArchivedAt = now;
        original.UpdatedAt = now;

        await CloseOpenPriceIntervalsAsync(original.TenantId, original.Id, now, cancellationToken)
            .ConfigureAwait(false);

        var newProduct = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = original.TenantId,
            Name = original.Name,
            NameDe = original.NameDe,
            NameEn = original.NameEn,
            NameTr = original.NameTr,
            Description = original.Description,
            DescriptionDe = original.DescriptionDe,
            DescriptionEn = original.DescriptionEn,
            DescriptionTr = original.DescriptionTr,
            Price = newPrice,
            TaxGroupId = request.NewTaxGroupId,
            TaxRate = newTaxRate,
            TaxType = TaxTypes.FromTaxGroup(newGroup),
            Category = original.Category,
            CategoryId = original.CategoryId,
            ImageUrl = original.ImageUrl,
            StockQuantity = original.StockQuantity,
            MinStockLevel = original.MinStockLevel,
            MaxStockLevel = original.MaxStockLevel,
            Unit = original.Unit,
            Cost = original.Cost,
            Barcode = activeBarcode,
            IsFiscalCompliant = original.IsFiscalCompliant,
            FiscalCategoryCode = original.FiscalCategoryCode,
            IsTaxable = original.IsTaxable,
            TaxExemptionReason = original.TaxExemptionReason,
            RksvProductType = original.RksvProductType,
            IsSellableAddOn = original.IsSellableAddOn,
            Version = original.Version + 1,
            OriginalProductId = original.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = original.CreatedBy,
            UpdatedBy = request.ChangedBy == Guid.Empty ? original.UpdatedBy : request.ChangedBy.ToString("D"),
        };

        // Stock moves to the live catalog row; archived row keeps history only.
        original.StockQuantity = 0;

        foreach (var assignment in original.ModifierGroupAssignments)
        {
            newProduct.ModifierGroupAssignments.Add(new ProductModifierGroupAssignment
            {
                ProductId = newProduct.Id,
                ModifierGroupId = assignment.ModifierGroupId,
                TenantId = assignment.TenantId,
                SortOrder = assignment.SortOrder,
            });
        }

        await _db.Products.AddAsync(newProduct, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _priceHistoryService.EnsureInitialVersionAsync(
            newProduct.TenantId,
            newProduct.Id,
            newProduct.Price,
            newProduct.TaxGroupId,
            newProduct.TaxRate,
            request.ChangedBy,
            reason: string.IsNullOrWhiteSpace(request.Reason)
                ? $"Catalog version {newProduct.Version} (supersedes {original.Id:D})"
                : request.Reason.Trim(),
            saveChanges: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created catalog product version {Version} {NewProductId} superseding {OldProductId} (tenant {TenantId})",
            newProduct.Version,
            newProduct.Id,
            original.Id,
            original.TenantId);

        return newProduct;
    }

    public async Task<IReadOnlyList<ProductPriceHistory>> GetPriceHistoryAsync(
        Guid tenantId,
        Guid productId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));

        var limit = Math.Clamp(take, 1, 500);
        return await _db.ProductPriceHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.ProductId == productId)
            .OrderByDescending(h => h.EffectiveFrom)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PriceChangeValidationResult> ValidatePriceChangeAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return PriceChangeValidationResult.Fail("Request is required.");
        if (request.TenantId == Guid.Empty)
            return PriceChangeValidationResult.Fail("Tenant id is required.");
        if (request.ProductId == Guid.Empty)
            return PriceChangeValidationResult.Fail("Product id is required.");
        if (request.NewTaxGroupId == Guid.Empty)
            return PriceChangeValidationResult.Fail("New tax group id is required.");
        if (request.NewPrice < 0)
            return PriceChangeValidationResult.Fail("Price cannot be negative.");
        if (request.NewPrice == 0)
            return PriceChangeValidationResult.Fail("Price must be greater than zero.");

        var compliance = await _complianceChecker.CheckPriceChangeComplianceAsync(
                request.TenantId,
                request.ProductId,
                request.NewPrice,
                request.NewTaxGroupId,
                request.ForceInPlaceUpdate,
                cancellationToken)
            .ConfigureAwait(false);

        if (!compliance.IsCompliant)
        {
            var error = compliance.Errors.Count > 0
                ? compliance.Errors[0].Message
                : "RKSV compliance check failed.";
            return PriceChangeValidationResult.Fail(error, compliance);
        }

        if (compliance.Warnings.Count > 0)
        {
            return PriceChangeValidationResult.Warn(
                compliance.Warnings[0].Message,
                hasFiscalHistory: compliance.HasFiscalHistory,
                requiresNewProductVersion: compliance.RequiresNewProductVersion,
                compliance: compliance);
        }

        return PriceChangeValidationResult.Success(
            hasFiscalHistory: compliance.HasFiscalHistory,
            requiresNewProductVersion: compliance.RequiresNewProductVersion,
            compliance: compliance);
    }

    private async Task<PriceChangeResult> ChangePriceViaNewProductVersionAsync(
        PriceChangeRequest request,
        PriceChangeValidationResult validation,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var original = await _db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.Id == request.ProductId && p.TenantId == request.TenantId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (original is null)
                    return PriceChangeResult.Fail("Product not found");

                var oldPrice = decimal.Round(original.Price, 2, MidpointRounding.AwayFromZero);
                var newPrice = decimal.Round(request.NewPrice, 2, MidpointRounding.AwayFromZero);
                var oldTaxGroupId = original.TaxGroupId;
                var oldTaxRate = decimal.Round(original.TaxRate, 2, MidpointRounding.AwayFromZero);

                if (oldPrice == newPrice && oldTaxGroupId == request.NewTaxGroupId && oldTaxRate ==
                    (await GetTaxRateAsync(request.TenantId, request.NewTaxGroupId, cancellationToken)
                        .ConfigureAwait(false)))
                {
                    return PriceChangeResult.Fail("Price and tax group are unchanged.");
                }

                var newProduct = await CreateNewProductVersionAsync(
                        request.ProductId,
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

                var currentVersion = await _db.ProductPriceVersions
                    .AsNoTracking()
                    .Where(v => v.TenantId == request.TenantId && v.ProductId == newProduct.Id && v.IsCurrent)
                    .OrderByDescending(v => v.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    var actorId = request.ChangedBy == Guid.Empty
                        ? "system"
                        : request.ChangedBy.ToString("D");
                    var actorRole = string.IsNullOrWhiteSpace(request.ChangedByRole)
                        ? "Manager"
                        : request.ChangedByRole.Trim();

                    await _auditLogService.LogSystemOperationAsync(
                        action: "PRODUCT_CATALOG_VERSION_CREATED",
                        entityType: "Product",
                        userId: actorId,
                        userRole: actorRole,
                        description: "New catalog product version created for RKSV compliance",
                        notes: request.Reason,
                        actionType: AuditEventType.ProductCatalogVersionCreated,
                        entityId: newProduct.Id,
                        tenantId: newProduct.TenantId,
                        oldValues: new
                        {
                            ProductId = original.Id,
                            Price = oldPrice,
                            TaxGroupId = oldTaxGroupId,
                            TaxRate = oldTaxRate,
                            Version = original.Version,
                        },
                        newValues: new
                        {
                            ProductId = newProduct.Id,
                            Price = newProduct.Price,
                            TaxGroupId = newProduct.TaxGroupId,
                            TaxRate = newProduct.TaxRate,
                            Version = newProduct.Version,
                            OriginalProductId = newProduct.OriginalProductId,
                            Reason = request.Reason,
                        },
                        entityName: newProduct.Name).ConfigureAwait(false);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(
                        auditEx,
                        "Audit log failed after catalog version create for product {ProductId}",
                        original.Id);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return PriceChangeResult.Success(
                    newProduct.Id,
                    currentVersion?.Id ?? Guid.Empty,
                    currentVersion?.Version ?? newProduct.Version.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    oldPrice,
                    newProduct.Price,
                    oldTaxGroupId,
                    newProduct.TaxGroupId,
                    oldTaxRate,
                    newProduct.TaxRate,
                    validation.WarningMessage,
                    createdNewProductVersion: true,
                    archivedProductId: original.Id,
                    catalogVersion: newProduct.Version);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Catalog product versioning failed for product {ProductId}", request.ProductId);
                return PriceChangeResult.Fail($"Price change failed: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    private async Task<PriceChangeResult> ChangePriceInPlaceAsync(
        PriceChangeRequest request,
        PriceChangeValidationResult validation,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(
                        p => p.Id == request.ProductId && p.TenantId == request.TenantId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (product is null)
                    return PriceChangeResult.Fail("Product not found");

                var newGroup = await _db.TaxGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        g => g.Id == request.NewTaxGroupId
                             && g.TenantId == request.TenantId
                             && g.IsActive,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (newGroup is null)
                    return PriceChangeResult.Fail("Tax group not found or inactive.");

                var oldPrice = decimal.Round(product.Price, 2, MidpointRounding.AwayFromZero);
                var newPrice = decimal.Round(request.NewPrice, 2, MidpointRounding.AwayFromZero);
                var oldTaxGroupId = product.TaxGroupId;
                var oldTaxRate = decimal.Round(product.TaxRate, 2, MidpointRounding.AwayFromZero);
                var newTaxRate = decimal.Round(newGroup.Rate, 2, MidpointRounding.AwayFromZero);

                if (oldPrice == newPrice && oldTaxGroupId == request.NewTaxGroupId && oldTaxRate == newTaxRate)
                    return PriceChangeResult.Fail("Price and tax group are unchanged.");

                product.Price = newPrice;
                product.TaxGroupId = request.NewTaxGroupId;
                product.TaxRate = newTaxRate;
                product.TaxType = TaxTypes.FromTaxGroup(newGroup);
                product.UpdatedAt = DateTime.UtcNow;

                if (oldTaxGroupId != request.NewTaxGroupId || oldTaxRate != newTaxRate)
                {
                    _db.TaxHistories.Add(new TaxHistory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        ProductId = product.Id,
                        TaxGroupId = request.NewTaxGroupId,
                        OldRate = oldTaxRate,
                        NewRate = newTaxRate,
                        ChangedAt = DateTime.UtcNow,
                        ChangedBy = request.ChangedBy == Guid.Empty ? Guid.Empty : request.ChangedBy,
                        Reason = string.IsNullOrWhiteSpace(request.Reason)
                            ? "Product tax group updated"
                            : request.Reason.Trim(),
                    });
                }

                var history = await _priceHistoryService.RecordChangeAsync(
                    request.TenantId,
                    product.Id,
                    oldPrice,
                    newPrice,
                    oldTaxGroupId,
                    request.NewTaxGroupId,
                    oldTaxRate,
                    newTaxRate,
                    request.ChangedBy,
                    reason: string.IsNullOrWhiteSpace(request.Reason)
                        ? "Product price updated"
                        : request.Reason.Trim(),
                    isRksvCompliant: true,
                    rksvNote: validation.HasFiscalHistory
                        ? "In-place price change logged for RKSV compliance (forced)."
                        : "Price change logged for RKSV compliance",
                    saveChanges: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var currentVersion = await _db.ProductPriceVersions
                    .AsNoTracking()
                    .Where(v => v.TenantId == request.TenantId && v.ProductId == product.Id && v.IsCurrent)
                    .OrderByDescending(v => v.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    var actorId = request.ChangedBy == Guid.Empty
                        ? "system"
                        : request.ChangedBy.ToString("D");
                    var actorRole = string.IsNullOrWhiteSpace(request.ChangedByRole)
                        ? "Manager"
                        : request.ChangedByRole.Trim();

                    await _auditLogService.LogSystemOperationAsync(
                        action: "PRODUCT_PRICE_CHANGED",
                        entityType: "Product",
                        userId: actorId,
                        userRole: actorRole,
                        description: "Product price and/or tax group changed",
                        notes: request.Reason,
                        actionType: AuditEventType.ProductPriceChanged,
                        entityId: product.Id,
                        tenantId: product.TenantId,
                        oldValues: new
                        {
                            Price = oldPrice,
                            TaxGroupId = oldTaxGroupId,
                            TaxRate = oldTaxRate,
                        },
                        newValues: new
                        {
                            Price = newPrice,
                            TaxGroupId = request.NewTaxGroupId,
                            TaxRate = newTaxRate,
                            Reason = request.Reason,
                            PriceVersionId = currentVersion?.Id,
                            Version = currentVersion?.Version,
                            HistoryId = history?.Id,
                        },
                        entityName: product.Name).ConfigureAwait(false);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(
                        auditEx,
                        "Audit log failed after price change for product {ProductId}",
                        product.Id);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "RKSV price change for product {ProductId}: {OldPrice} → {NewPrice}, tax {OldRate}% → {NewRate}% (tenant {TenantId})",
                    product.Id,
                    oldPrice,
                    newPrice,
                    oldTaxRate,
                    newTaxRate,
                    request.TenantId);

                return PriceChangeResult.Success(
                    product.Id,
                    currentVersion?.Id ?? Guid.Empty,
                    currentVersion?.Version,
                    oldPrice,
                    newPrice,
                    oldTaxGroupId,
                    request.NewTaxGroupId,
                    oldTaxRate,
                    newTaxRate,
                    validation.WarningMessage,
                    catalogVersion: product.Version);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Price change failed for product {ProductId}", request.ProductId);
                return PriceChangeResult.Fail($"Price change failed: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    private async Task CloseOpenPriceIntervalsAsync(
        Guid tenantId,
        Guid productId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeHistory = await _db.ProductPriceHistories
            .Where(h => h.TenantId == tenantId && h.ProductId == productId && h.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in activeHistory)
        {
            row.IsActive = false;
            row.EffectiveTo = now;
        }

        var currentVersions = await _db.ProductPriceVersions
            .Where(v => v.TenantId == tenantId && v.ProductId == productId && v.IsCurrent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in currentVersions)
        {
            row.IsCurrent = false;
            row.ValidTo = now;
        }
    }

    private async Task<decimal> GetTaxRateAsync(
        Guid tenantId,
        Guid taxGroupId,
        CancellationToken cancellationToken)
    {
        var rate = await _db.TaxGroups
            .AsNoTracking()
            .Where(g => g.Id == taxGroupId && g.TenantId == tenantId)
            .Select(g => (decimal?)g.Rate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return decimal.Round(rate ?? 0m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Builds a unique archived barcode that frees the live POS/scannable code.</summary>
    internal static string BuildArchivedBarcode(string barcode, int version, Guid productId)
    {
        var baseCode = string.IsNullOrWhiteSpace(barcode) ? "NOBC" : barcode.Trim();
        var suffix = $"__v{version}_{productId.ToString("N")[..8]}";
        const int maxLen = 50;
        if (baseCode.Length + suffix.Length <= maxLen)
            return baseCode + suffix;

        var keep = Math.Max(1, maxLen - suffix.Length);
        return baseCode[..keep] + suffix;
    }
}
