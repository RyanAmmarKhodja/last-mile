import { Link } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert } from '@/shared/components';
import { ProfileCard } from '../components/ProfileCard';
import { RoleCapabilitiesCard } from '../components/RoleCapabilitiesCard';
import { AccessCheckCard } from '../components/AccessCheckCard';
import { TokenCard } from '../components/TokenCard';
import '../styles/auth.css';

export function HomePage() {
  const { user, hasRole } = useAuth();
  if (!user) return null;

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Welcome, {user.email.split('@')[0]}</h1>
        <p>You are signed in. This page shows your account and what your role lets you do.</p>
      </div>

      {hasRole('ADMIN') && (
        <Alert variant="info">
          You are an administrator. Manage accounts on the <Link to="/admin/users">Users</Link> page.
        </Alert>
      )}

      <div className="grid">
        <ProfileCard user={user} />
        <RoleCapabilitiesCard role={user.role} />
      </div>
      <AccessCheckCard user={user} />
      <TokenCard />
    </div>
  );
}
