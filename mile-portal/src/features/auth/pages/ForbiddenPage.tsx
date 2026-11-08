import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import { Card, RoleBadge } from '@/shared/components';
import type { UserRole } from '@/shared/types/auth';

interface LocationState {
  from?: string;
  required?: UserRole[];
}

export function ForbiddenPage() {
  const { user } = useAuth();
  const { from, required } = (useLocation().state ?? {}) as LocationState;

  return (
    <Card className="narrow" title="403 · Access denied">
      <p>
        {from ? (
          <>
            <code>{from}</code> requires{' '}
          </>
        ) : (
          'This page requires '
        )}
        {required?.length ? required.map((r) => <RoleBadge key={r} role={r} />) : 'a different role'}.
      </p>
      {user && (
        <p>
          You are signed in as <strong>{user.email}</strong> with role <RoleBadge role={user.role} />.
        </p>
      )}
      <Link to="/home">Back to home</Link>
    </Card>
  );
}
