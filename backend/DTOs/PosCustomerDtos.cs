namespace KasseAPI_Final.DTOs;

/// <summary>POS customer lookup response (QR scan).</summary>
public sealed class PosCustomerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CustomerNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int LoyaltyPoints { get; init; }
    public PosCustomerPetDataDto? PetData { get; init; }
    public PosCustomerAddressDataDto? AddressData { get; init; }
}

public sealed class PosCustomerPetDataDto
{
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? PetName { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? PetSpecies { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? PetBreed { get; init; }
    public DateOnly? PetBirthDate { get; init; }
}

public sealed class PosCustomerAddressDataDto
{
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Street { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string? PostalCode { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? City { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Notes { get; init; }
}

public sealed class CreatePosCustomerRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Email { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string? Phone { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Address { get; init; }

    public PosCustomerPetDataDto? PetData { get; init; }

    public PosCustomerAddressDataDto? AddressData { get; init; }
}

public sealed class UpdatePosCustomerAddressRequest
{
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Address { get; init; }

    public PosCustomerAddressDataDto? AddressData { get; init; }
}
