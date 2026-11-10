import type { RegistrationProfile, UserRole } from '@/shared/types/auth';

/** Client-side mirror of AuthService's rules (Core/User.cs) for instant feedback. The server re-checks everything. */
export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;
export const PASSWORD_HINT = `${PASSWORD_MIN}–${PASSWORD_MAX} characters, at least one letter and one digit.`;

export function validateEmail(email: string): string | null {
  const value = email.trim();
  if (!value) return 'Email is required.';
  if (value.length > 255) return 'Email is too long.';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return 'Enter a valid email address.';
  return null;
}

export function validatePassword(password: string): string | null {
  if (!password) return 'Password is required.';
  if (password.length < PASSWORD_MIN || password.length > PASSWORD_MAX)
    return `Password must be between ${PASSWORD_MIN} and ${PASSWORD_MAX} characters.`;
  if (!/\p{L}/u.test(password) || !/\d/.test(password)) return 'Password must contain at least one letter and one digit.';
  return null;
}

export function validateConfirmation(password: string, confirmation: string): string | null {
  return password === confirmation ? null : 'Passwords do not match.';
}

/** Form state for the registration profile: every field is a string, empty = not provided. */
export type ProfileForm = Required<RegistrationProfile>;
export type ProfileErrors = Partial<Record<keyof RegistrationProfile, string>>;

export const EMPTY_PROFILE: ProfileForm = {
  firstName: '',
  lastName: '',
  phoneNumber: '',
  companyName: '',
  address: '',
  licenseNumber: '',
};

/** Which profile fields a role uses. Mirrors AuthService Core/RegistrationProfileRules.cs. */
export const PROFILE_FIELDS: Record<UserRole, { field: keyof RegistrationProfile; required: boolean }[]> = {
  CLIENT: [
    { field: 'firstName', required: true },
    { field: 'lastName', required: true },
    { field: 'phoneNumber', required: true },
    { field: 'companyName', required: false },
    { field: 'address', required: true },
  ],
  DRIVER: [
    { field: 'firstName', required: true },
    { field: 'lastName', required: true },
    { field: 'phoneNumber', required: true },
    { field: 'licenseNumber', required: true },
  ],
  ADMIN: [
    { field: 'firstName', required: true },
    { field: 'lastName', required: true },
    { field: 'phoneNumber', required: false },
  ],
};

const MAX_LENGTH: Record<keyof RegistrationProfile, number> = {
  firstName: 100,
  lastName: 100,
  phoneNumber: 50,
  companyName: 255,
  address: 1000,
  licenseNumber: 100,
};

export function validateProfile(role: UserRole, profile: ProfileForm): ProfileErrors {
  const errors: ProfileErrors = {};
  for (const { field, required } of PROFILE_FIELDS[role]) {
    const value = profile[field].trim();
    if (required && !value) errors[field] = 'Required.';
    else if (value.length > MAX_LENGTH[field]) errors[field] = `At most ${MAX_LENGTH[field]} characters.`;
  }
  return errors;
}

/** Request payload: trimmed, only the fields that apply to the role, empty optional fields omitted. */
export function toProfilePayload(role: UserRole, profile: ProfileForm): RegistrationProfile {
  const payload: RegistrationProfile = { firstName: profile.firstName.trim(), lastName: profile.lastName.trim() };
  for (const { field } of PROFILE_FIELDS[role]) {
    const value = profile[field].trim();
    if (value) payload[field] = value;
  }
  return payload;
}
