import { NavLink } from 'react-router-dom';

/** Route-backed tabs (each tab is a link, so it can be bookmarked). */
export function Tabs({ items }: { items: { to: string; label: string; end?: boolean }[] }) {
  return (
    <nav className="tabs" aria-label="Sections">
      {items.map((t) => (
        <NavLink key={t.to} to={t.to} end={t.end} className={({ isActive }) => ['tab', isActive && 'active'].filter(Boolean).join(' ')}>
          {t.label}
        </NavLink>
      ))}
    </nav>
  );
}
