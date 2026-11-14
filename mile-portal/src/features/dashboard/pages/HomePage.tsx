import { useAuth } from '@/shared/context/AuthContext';
import { AdminDashboard } from './AdminDashboard';
import { ClientDashboard } from './ClientDashboard';
import { DriverWorkspace } from './DriverWorkspace';

/** /home: one dashboard per role (§5). */
export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  if (user.role === 'ADMIN') return <AdminDashboard />;
  if (user.role === 'DRIVER') return <DriverWorkspace />;
  return <ClientDashboard />;
}
