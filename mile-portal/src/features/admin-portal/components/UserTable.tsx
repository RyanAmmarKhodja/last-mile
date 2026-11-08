import { Button, RoleBadge, StatusBadge } from '@/shared/components';
import { USER_ROLES, type User, type UserRole } from '@/shared/types/auth';
import { formatDateTime } from '@/shared/utils/format';

interface Props {
  users: User[];
  currentUserId: string;
  busyId: string | null;
  onChangeRole: (user: User, role: UserRole) => void;
  onToggleActive: (user: User) => void;
  onResetPassword: (user: User) => void;
  onDelete: (user: User) => void;
}

export function UserTable({ users, currentUserId, busyId, onChangeRole, onToggleActive, onResetPassword, onDelete }: Props) {
  if (users.length === 0) return <p className="muted">No users match these filters.</p>;

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th>Email</th>
            <th>Role</th>
            <th>Status</th>
            <th>Created</th>
            <th aria-label="Actions" />
          </tr>
        </thead>
        <tbody>
          {users.map((u) => {
            const isSelf = u.id === currentUserId;
            const busy = busyId === u.id;
            return (
              <tr key={u.id} className={isSelf ? 'is-self' : undefined}>
                <td>
                  <div className="user-email">
                    {u.email} {isSelf && <span className="muted">(you)</span>}
                  </div>
                  <code className="muted user-id">{u.id}</code>
                </td>
                <td>
                  <div className="row">
                    <RoleBadge role={u.role} />
                    <select className="input input-sm" value={u.role} disabled={busy} aria-label={`Role of ${u.email}`}
                      onChange={(e) => onChangeRole(u, e.target.value as UserRole)}>
                      {USER_ROLES.map((r) => (
                        <option key={r} value={r}>
                          {r}
                        </option>
                      ))}
                    </select>
                  </div>
                </td>
                <td>
                  <StatusBadge active={u.isActive} />
                </td>
                <td className="nowrap">{formatDateTime(u.createdAt)}</td>
                <td>
                  <div className="row nowrap-row">
                    <Button size="sm" variant="secondary" disabled={busy} onClick={() => onToggleActive(u)}>
                      {u.isActive ? 'Disable' : 'Enable'}
                    </Button>
                    <Button size="sm" variant="secondary" disabled={busy} onClick={() => onResetPassword(u)}>
                      Reset password
                    </Button>
                    <Button size="sm" variant="danger" disabled={busy} onClick={() => onDelete(u)}>
                      Delete
                    </Button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
