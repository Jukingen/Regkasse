using KasseAPI_Final.Models;

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

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Species { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Breed { get; init; }

    public DateOnly? BirthDate { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Vaccines { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Allergies { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? ChronicConditions { get; init; }

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Insurance { get; init; }

    public CustomerPetData? ToEntity()
    {
        var entity = new CustomerPetData
        {
            PetName = Trim(PetName),
            PetSpecies = Trim(PetSpecies),
            PetBreed = Trim(PetBreed),
            PetBirthDate = PetBirthDate,
            Species = Trim(Species),
            Breed = Trim(Breed),
            BirthDate = BirthDate,
            Vaccines = Trim(Vaccines),
            Allergies = Trim(Allergies),
            ChronicConditions = Trim(ChronicConditions),
            Insurance = Trim(Insurance),
        };
        entity.AlignLegacyKeys();
        return entity.HasContent() ? entity : null;
    }

    public static PosCustomerPetDataDto? FromEntity(CustomerPetData? entity)
    {
        if (entity is null || !entity.HasContent())
            return null;
        entity.AlignLegacyKeys();
        return new PosCustomerPetDataDto
        {
            PetName = entity.PetName,
            PetSpecies = entity.PetSpecies,
            PetBreed = entity.PetBreed,
            PetBirthDate = entity.PetBirthDate,
            Species = entity.Species,
            Breed = entity.Breed,
            BirthDate = entity.BirthDate,
            Vaccines = entity.Vaccines,
            Allergies = entity.Allergies,
            ChronicConditions = entity.ChronicConditions,
            Insurance = entity.Insurance,
        };
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
