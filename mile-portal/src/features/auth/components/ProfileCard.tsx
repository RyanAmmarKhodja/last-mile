import type { User } from '@/shared/types/auth';
import { Card, RoleBadge, StatusBadge } from '@/shared/components';
import { formatDateTime } from '@/shared/utils/format';

/** The backend has no separate username: the email is the login identifier. */
export function ProfileCard({ user }: { user: User }) {
  const username = user.email.split('@')[0];
  return (
    <Card title="Profile" subtitle="From GET /api/v1/users/me">
      <div className="profile-head">
        <div className="avatar">{username.slice(0, 2).toUpperCase()}</div>
        <div>
          <div className="profile-name">{username}</div>
          <div className="row">
            <RoleBadge role={user.role} />
            <StatusBadge active={user.isActive} />
          </div>
        </div>
      </div>
      <dl className="details">
        <dt>Email</dt>
        <dd>{user.email}</dd>
        <dt>Role</dt>
        <dd>{user.role}</dd>
        <dt>User ID</dt>
        <dd>
          <code>{user.id}</code>
        </dd>
        <dt>Created</dt>
        <dd>{formatDateTime(user.createdAt)}</dd>
        <dt>Last updated</dt>
        <dd>{formatDateTime(user.updatedAt)}</dd>
      </dl>
    </Card>
  );
}
