import { Link, Outlet } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import '../styles/public.css';

/** Shell for pages that need no account (FR-3). */
export function PublicLayout() {
  const { status } = useAuth();
  return (
    <div className="public-layout">
      <header className="public-header">
        <Link to="/track" className="navbar-brand">
          <span className="brand-mark sm">LM</span>
          <span>Last Mile tracking</span>
        </Link>
        <Link to={status === 'authenticated' ? '/home' : '/login'}>{status === 'authenticated' ? 'My dashboard' : 'Sign in'}</Link>
      </header>
      <main className="public-main">
        <Outlet />
      </main>
    </div>
  );
}
