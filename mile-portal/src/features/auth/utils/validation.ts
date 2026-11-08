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
