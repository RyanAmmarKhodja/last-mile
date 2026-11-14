import { NavLink } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import type { UserRole } from '@/shared/types/auth';

interface NavItem {
  to: string;
  label: string;
  end?: boolean;
}
interface NavSection {
  title: string;
  items: NavItem[];
}

/** P7: each role sees only what it can open. The route guards and the backend still enforce it. */
const NAV: Record<UserRole, NavSection[]> = {
  ADMIN: [
    {
      title: 'Operations',
      items: [
        { to: '/home', label: 'Dashboard' },
        { to: '/map', label: 'Live map' },
        { to: '/shipments', label: 'Shipments' },
      ],
    },
    {
      title: 'Manage',
      items: [
        { to: '/fleet/drivers', label: 'Fleet' },
        { to: '/admin/users', label: 'Users' },
      ],
    },
    { title: 'You', items: [{ to: '/account', label: 'Account' }] },
  ],
  CLIENT: [
    {
      title: 'Deliveries',
      items: [
        { to: '/home', label: 'My deliveries' },
        { to: '/shipments', label: 'Shipments', end: true },
        { to: '/shipments/new', label: 'Send a parcel' },
      ],
    },
    { title: 'You', items: [{ to: '/account', label: 'Account' }] },
  ],
  DRIVER: [
    {
      title: 'Work',
      items: [
        { to: '/home', label: 'Workspace' },
        { to: '/shipments', label: 'My deliveries' },
      ],
    },
    { title: 'You', items: [{ to: '/account', label: 'Account' }] },
  ],
};

export function Sidebar() {
  const { user } = useAuth();
  if (!user) return null;
  return (
    <nav className="sidebar" aria-label="Main">
      {NAV[user.role].map((section) => (
        <div key={section.title} style={{ display: 'contents' }}>
          <div className="sidebar-section">{section.title}</div>
          {section.items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => ['sidebar-link', isActive && 'active'].filter(Boolean).join(' ')}
            >
              {item.label}
            </NavLink>
          ))}
        </div>
      ))}
      <div className="sidebar-section">Public</div>
      <a className="sidebar-link" href="/track" target="_blank" rel="noreferrer">
        Track a parcel ↗
      </a>
    </nav>
  );
}
