using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IPosCustomerQrLookupService
{
    Task<PosCustomerDto?> ResolveByQrDataAsync(string? qrData, CancellationToken cancellationToken = default);
}

public sealed class PosCustomerQrLookupService : IPosCustomerQrLookupService
{
    private readonly AppDbContext _context;

    public PosCustomerQrLookupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PosCustomerDto?> ResolveByQrDataAsync(string? qrData, CancellationToken cancellationToken = default)
    {
        var trimmed = (qrData ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var parsed = CustomerQrPayloadParser.Parse(trimmed);
        Customer? customer = null;

        if (parsed.Ok)
        {
            customer = await FindByParsedAsync(parsed, cancellationToken);
        }

        customer ??= await FindByRawQrPayloadAsync(trimmed, cancellationToken);

        return customer == null ? null : MapDto(customer);
    }

    private async Task<Customer?> FindByParsedAsync(CustomerQrParseResult parsed, CancellationToken cancellationToken)
    {
        if (parsed.CustomerId.HasValue)
        {
            return await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parsed.CustomerId.Value && c.IsActive, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(parsed.CustomerNumber))
        {
            return await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerNumber == parsed.CustomerNumber && c.IsActive,
                    cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(parsed.Email))
        {
            var email = parsed.Email.Trim();
            return await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && c.Email.ToLower() == email.ToLower(),
                    cancellationToken);
        }

        return null;
    }

    /// <summary>Exact QR payload match (stored QR value / canonical formats).</summary>
    private async Task<Customer?> FindByRawQrPayloadAsync(string qrData, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(qrData, out var directId))
        {
            var byId = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == directId && c.IsActive, cancellationToken);
            if (byId != null)
                return byId;
        }

        var byNumber = await _context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive && c.CustomerNumber == qrData, cancellationToken);
        if (byNumber != null)
            return byNumber;

        if (qrData.Contains('@', StringComparison.Ordinal))
        {
            var byEmail = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && c.Email.ToLower() == qrData.ToLower(),
                    cancellationToken);
            if (byEmail != null)
                return byEmail;
        }

        // Canonical payload forms (equivalent to dedicated QRCode column).
        var candidates = await _context.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.CustomerNumber, c.Email })
            .ToListAsync(cancellationToken);

        foreach (var row in candidates)
        {
            if (string.Equals($"RK:C:{row.CustomerNumber}", qrData, StringComparison.OrdinalIgnoreCase))
                return await LoadByIdAsync(row.Id, cancellationToken);
            if (string.Equals($"RK:CU:{row.Id}", qrData, StringComparison.OrdinalIgnoreCase))
                return await LoadByIdAsync(row.Id, cancellationToken);
            if (string.Equals($"customer:{row.Id}", qrData, StringComparison.OrdinalIgnoreCase))
                return await LoadByIdAsync(row.Id, cancellationToken);
            if (!string.IsNullOrEmpty(row.Email)
                && string.Equals($"customer:{row.Email}", qrData, StringComparison.OrdinalIgnoreCase))
                return await LoadByIdAsync(row.Id, cancellationToken);
        }

        return null;
    }

    private Task<Customer?> LoadByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);

    private static PosCustomerDto MapDto(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        CustomerNumber = customer.CustomerNumber,
        Email = customer.Email,
        Phone = customer.Phone,
        LoyaltyPoints = customer.LoyaltyPoints,
    };
}
