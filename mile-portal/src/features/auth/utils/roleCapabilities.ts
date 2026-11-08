import type { UserRole } from '@/shared/types/auth';

interface RoleInfo {
  summary: string;
  can: string[];
  cannot: string[];
}

/** What each role is allowed to do in AuthService today (see UsersController / UserService). */
export const ROLE_CAPABILITIES: Record<UserRole, RoleInfo> = {
  ADMIN: {
    summary: 'Platform administrator. Manages every account.',
    can: [
      'View, search and filter all users (GET /users)',
      'Create accounts of any role, including ADMIN',
      "Change any user's role, email, password or active status",
      'Delete any account (except the last active admin)',
    ],
    cannot: ['Demote, disable or delete the last active administrator'],
  },
  DRIVER: {
    summary: 'Delivery driver. Will report GPS telemetry and handle assigned shipments.',
    can: ['View and edit own profile (email, password)', 'Delete own account'],
    cannot: ['List or view other users', 'Change own role or active status', 'Open the admin Users page'],
  },
  CLIENT: {
    summary: 'Customer. Will place shipments and track deliveries on the map.',
    can: ['View and edit own profile (email, password)', 'Delete own account'],
    cannot: ['List or view other users', 'Change own role or active status', 'Open the admin Users page'],
  },
};
