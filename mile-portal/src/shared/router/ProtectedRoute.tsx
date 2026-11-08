import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import { FullPageSpinner } from '@/shared/components';
import type { UserRole } from '@/shared/types/auth';

interface Props {
  /** When set, the user must hold one of these roles; otherwise they are sent to /forbidden. */
  roles?: UserRole[];
}

export function ProtectedRoute({ roles }: Props) {
  const { status, user } = useAuth();
  const location = useLocation();

  if (status === 'loading') return <FullPageSpinner />;
  if (status === 'anonymous' || !user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (roles && !roles.includes(user.role))
    return <Navigate to="/forbidden" replace state={{ from: location.pathname, required: roles }} />;

  return <Outlet />;
}

/** For /login and /register: signed-in users go back where they came from (or /home). */
export function PublicOnlyRoute() {
  const { status } = useAuth();
  const from = (useLocation().state as { from?: string } | null)?.from;
  if (status === 'loading') return <FullPageSpinner />;
  if (status === 'authenticated') return <Navigate to={from ?? '/home'} replace />;
  return <Outlet />;
}
