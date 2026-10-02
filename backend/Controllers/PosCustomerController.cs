using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>POS customer lookup and tenant-scoped customer creation. Benefits are applied at payment.</summary>
[Authorize]
[ApiController]
[Route("api/pos/customers")]
[HasPermission(AppPermissions.CustomerView)]
public sealed class PosCustomerController : BaseController
{
    private readonly IPosCustomerQrLookupService _qrLookup;
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosCustomerController(
        IPosCustomerQrLookupService qrLookup,
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<PosCustomerController> logger)
        : base(logger)
    {
        _qrLookup = qrLookup;
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Create a tenant-scoped POS customer, including optional veterinary pet data.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.CustomerManage)]
    [ProducesResponseType(typeof(PosCustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PosCustomerDto>> CreateCustomer(
        [FromBody] CreatePosCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length > 0
            && await _db.Customers.AnyAsync(
                customer => customer.Email == email && customer.IsActive,
                cancellationToken))
        {
            return Conflict(new { message = "This email address is already in use." });
        }

        var customer = new Customer
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Email = email,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty,
            IsActive = true,
            IsSystem = false,
            PetData = request.PetData is null
                ? null
                : new CustomerPetData
                {
                    PetName = request.PetData.PetName?.Trim(),
                    PetSpecies = request.PetData.PetSpecies?.Trim(),
                    PetBreed = request.PetData.PetBreed?.Trim(),
                    PetBirthDate = request.PetData.PetBirthDate,
                },
            AddressData = MapAddress(request.AddressData),
        };
        if (string.IsNullOrWhiteSpace(customer.Address))
            customer.Address = FormatAddress(customer.AddressData) ?? string.Empty;
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = MapCustomer(customer);
        return Created($"/api/pos/customers/by-qr?qrData={customer.Id:D}", dto);
    }

    /// <summary>Update structured service address for an existing POS customer.</summary>
    [HttpPatch("{id:guid}")]
    [HasPermission(AppPermissions.CustomerManage)]
    [ProducesResponseType(typeof(PosCustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosCustomerDto>> UpdateCustomerAddress(
        Guid id,
        [FromBody] UpdatePosCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var customer = await _db.Customers
            .FirstOrDefaultAsync(row => row.Id == id && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (customer is null || customer.IsSystem)
            return NotFound();

        customer.AddressData = MapAddress(request.AddressData);
        var formatted = FormatAddress(customer.AddressData);
        customer.Address = string.IsNullOrWhiteSpace(request.Address)
            ? formatted ?? customer.Address
            : request.Address.Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(MapCustomer(customer));
    }

    /// <summary>Resolve customer from scanned QR payload (customer:, RK:C:, RK:CU:, regkasse://, number, email).</summary>
    [HttpGet("by-qr")]
    [ProducesResponseType(typeof(PosCustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosCustomerDto>> GetCustomerByQr(
        [FromQuery] string qrData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrData))
            return BadRequest(new { message = "qrData is required" });

        var parsed = CustomerQrPayloadParser.Parse(qrData);
        if (!parsed.Ok && !LooksLikeRawLookupToken(qrData))
            return BadRequest(new { message = parsed.Error });

        var customer = await _qrLookup.ResolveByQrDataAsync(qrData, cancellationToken);
        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    /// <summary>Legacy POST alias for QR lookup (same resolution as GET by-qr).</summary>
    [HttpPost("qr-lookup")]
    [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QrLookup(
        [FromBody] CustomerQrLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dto = await _qrLookup.ResolveByQrDataAsync(request.QrPayload, cancellationToken);
        if (dto == null)
            return NotFound(new { message = "Customer not found" });

        return SuccessResponse(MapLegacyCustomer(dto), "Customer retrieved successfully");
    }

    private static bool LooksLikeRawLookupToken(string qrData)
    {
        var trimmed = qrData.Trim();
        return Guid.TryParse(trimmed, out _)
               || trimmed.Contains('@', StringComparison.Ordinal)
               || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9_-]{1,20}$");
    }

    private static Customer MapLegacyCustomer(PosCustomerDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        CustomerNumber = dto.CustomerNumber,
        Email = dto.Email,
        Phone = dto.Phone,
        Address = dto.Address,
        LoyaltyPoints = dto.LoyaltyPoints,
        AddressData = MapAddress(dto.AddressData),
        PetData = dto.PetData is null
            ? null
            : new CustomerPetData
            {
                PetName = dto.PetData.PetName,
                PetSpecies = dto.PetData.PetSpecies,
                PetBreed = dto.PetData.PetBreed,
                PetBirthDate = dto.PetData.PetBirthDate,
            },
        IsActive = true,
    };

    private static PosCustomerDto MapCustomer(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        CustomerNumber = customer.CustomerNumber,
        Email = customer.Email,
        Phone = customer.Phone,
        Address = customer.Address,
        LoyaltyPoints = customer.LoyaltyPoints,
        PetData = customer.PetData is null
            ? null
            : new PosCustomerPetDataDto
            {
                PetName = customer.PetData.PetName,
                PetSpecies = customer.PetData.PetSpecies,
                PetBreed = customer.PetData.PetBreed,
                PetBirthDate = customer.PetData.PetBirthDate,
            },
        AddressData = customer.AddressData is null
            ? null
            : new PosCustomerAddressDataDto
            {
                Street = customer.AddressData.Street,
                PostalCode = customer.AddressData.PostalCode,
                City = customer.AddressData.City,
                Notes = customer.AddressData.Notes,
            },
    };

    private static CustomerAddressData? MapAddress(PosCustomerAddressDataDto? dto)
    {
        if (dto is null)
            return null;
        var street = dto.Street?.Trim();
        var postal = dto.PostalCode?.Trim();
        var city = dto.City?.Trim();
        var notes = dto.Notes?.Trim();
        if (string.IsNullOrEmpty(street)
            && string.IsNullOrEmpty(postal)
            && string.IsNullOrEmpty(city)
            && string.IsNullOrEmpty(notes))
            return null;
        return new CustomerAddressData
        {
            Street = street,
            PostalCode = postal,
            City = city,
            Notes = notes,
        };
    }

    private static string? FormatAddress(CustomerAddressData? data)
    {
        if (data is null)
            return null;
        var line1 = data.Street?.Trim();
        var line2 = string.Join(' ', new[] { data.PostalCode, data.City }
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value)));
        var formatted = string.Join(", ", new[] { line1, line2 }.Where(value => !string.IsNullOrEmpty(value)));
        return formatted.Length == 0 ? null : formatted;
    }
}
