import { Outlet, useLocation } from 'react-router-dom';
import { Navbar } from './Navbar';
import { Sidebar } from './Sidebar';

/** Full-bleed pages (the fleet map) drop the content padding and width cap. */
const FULL_BLEED = ['/map'];

/** Shell for signed-in pages: top navbar, left sidebar, routed content. */
export function AppLayout() {
  const { pathname } = useLocation();
  const full = FULL_BLEED.includes(pathname);
  return (
    <div className="app-layout">
      <Navbar />
      <div className="app-body">
        <Sidebar />
        <main className={['app-content', full && 'app-content-full'].filter(Boolean).join(' ')}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
