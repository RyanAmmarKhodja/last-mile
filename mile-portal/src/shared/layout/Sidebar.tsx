import { NavLink } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import type { UserRole } from '@/shared/types/auth';

interface NavItem {
  to: string;
  label: string;
  /** Roles allowed to open the page. Omitted = every signed-in user. */
  roles?: UserRole[];
}

const NAV_ITEMS: NavItem[] = [
  { to: '/home', label: 'Home' },
  { to: '/account', label: 'Account settings' },
  { to: '/admin/users', label: 'Users', roles: ['ADMIN'] },
];

/**
 * Restricted entries stay visible (with a lock) for roles that can't open them, so you can
 * click them and watch the route guard send you to /forbidden.
 */
export function Sidebar() {
  const { hasRole } = useAuth();
  return (
    <nav className="sidebar">
      {NAV_ITEMS.map((item) => {
        const allowed = !item.roles || hasRole(...item.roles);
        return (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) => ['sidebar-link', isActive && 'active', !allowed && 'locked'].filter(Boolean).join(' ')}
            title={allowed ? undefined : `Requires ${item.roles!.join(' or ')}`}
          >
            <span>{item.label}</span>
            {item.roles && <span className="sidebar-tag">{allowed ? item.roles.join('/') : `🔒 ${item.roles.join('/')}`}</span>}
          </NavLink>
        );
      })}
    </nav>
  );
}
