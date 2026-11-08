import type { UserRole } from '@/shared/types/auth';

export function RoleBadge({ role }: { role: UserRole | string }) {
  return <span className={`badge badge-role-${role.toLowerCase()}`}>{role}</span>;
}

export function StatusBadge({ active }: { active: boolean }) {
  return <span className={`badge ${active ? 'badge-ok' : 'badge-off'}`}>{active ? 'Active' : 'Disabled'}</span>;
}
