import { Link } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import { Button, RoleBadge } from '@/shared/components';

export function Navbar() {
  const { user, logout } = useAuth();
  return (
    <header className="navbar">
      <Link to="/home" className="navbar-brand">
        <span className="brand-mark sm">LM</span>
        <span>Last Mile Portal</span>
      </Link>
      {user && (
        <div className="navbar-user">
          <span className="navbar-email" title={user.email}>
            {user.email}
          </span>
          <RoleBadge role={user.role} />
          <Button variant="ghost" size="sm" onClick={() => logout()}>
            Sign out
          </Button>
        </div>
      )}
    </header>
  );
}
