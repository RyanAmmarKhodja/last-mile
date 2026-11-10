/** Mirrors AuthService's UserRole enum (M2M is reserved for service clients and never assigned to humans). */
export const USER_ROLES = ['ADMIN', 'DRIVER', 'CLIENT'] as const;
export type UserRole = (typeof USER_ROLES)[number];

/** Roles a visitor may pick on /register. ADMIN accounts can only be created by an admin. */
export const SELF_REGISTER_ROLES: readonly UserRole[] = ['CLIENT', 'DRIVER'];

/** UserDto */
export interface User {
  id: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

/** TokenResponseDto */
export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  refreshTokenExpiresAt: string;
}

/** PagedResultDto<T> */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** RFC 7807 body returned by AuthService for domain and validation errors. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * RegistrationProfileDto: relayed by AuthService to ProfileService / FleetService in the UserRegistered event.
 * Required per role: everyone → firstName, lastName · CLIENT → phoneNumber, address · DRIVER → phoneNumber, licenseNumber.
 * Fields that do not apply to the role must be omitted.
 */
export interface RegistrationProfile {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  companyName?: string;
  address?: string;
  licenseNumber?: string;
}

/** RegisterDto (POST /auth/register) and CreateUserDto (POST /users). */
export interface RegisterRequest {
  email: string;
  password: string;
  role: UserRole;
  profile: RegistrationProfile;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** UpdateUserDto: omitted fields are left unchanged. role / isActive are admin-only. */
export interface UpdateUserRequest {
  email?: string;
  password?: string;
  currentPassword?: string;
  role?: UserRole;
  isActive?: boolean;
}

export interface ListUsersParams {
  page?: number;
  pageSize?: number;
  role?: UserRole;
  search?: string;
}
