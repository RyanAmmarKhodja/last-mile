using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using Contract = LastMile.Contracts.Auth;

namespace LastMile.Auth.Core;

/// <summary>
/// Per-role rules for the registration profile (documents/data-ownership-matrix.md §8.1). AuthService checks shape
/// and required fields only. Business uniqueness (e.g. license numbers) is checked by the owning service.
/// </summary>
public static class RegistrationProfileRules
{
    public static Contract.RegistrationProfile Validate(UserRole role, RegistrationProfileDto? dto)
    {
        if (dto is null) throw new DomainValidationException("A profile is required.");

        var firstName = Clean(dto.FirstName);
        var lastName = Clean(dto.LastName);
        var phone = Clean(dto.PhoneNumber);
        var company = Clean(dto.CompanyName);
        var address = Clean(dto.Address);
        var license = Clean(dto.LicenseNumber);

        var errors = new List<string>();
        if (firstName is null) errors.Add("First name is required.");
        if (lastName is null) errors.Add("Last name is required.");

        switch (role)
        {
            case UserRole.CLIENT:
                if (phone is null) errors.Add("Phone number is required for clients.");
                if (address is null) errors.Add("Address is required for clients.");
                if (license is not null) errors.Add("License number only applies to drivers.");
                break;
            case UserRole.DRIVER:
                if (phone is null) errors.Add("Phone number is required for drivers.");
                if (license is null) errors.Add("License number is required for drivers.");
                if (address is not null || company is not null) errors.Add("Address and company only apply to clients.");
                break;
            case UserRole.ADMIN:
                if (address is not null || company is not null || license is not null)
                    errors.Add("Address, company and license number do not apply to administrators.");
                break;
            default:
                errors.Add($"Role {role} has no profile.");
                break;
        }

        if (errors.Count > 0) throw new DomainValidationException(string.Join(' ', errors));
        return new Contract.RegistrationProfile(firstName!, lastName!, phone, company, address, license);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
