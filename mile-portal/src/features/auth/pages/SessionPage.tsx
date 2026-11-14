import { useAuth } from '@/shared/context/AuthContext';
import { ProfileCard } from '../components/ProfileCard';
import { RoleCapabilitiesCard } from '../components/RoleCapabilitiesCard';
import { AccessCheckCard } from '../components/AccessCheckCard';
import { TokenCard } from '../components/TokenCard';
import { AccountTabs } from '../components/AccountTabs';
import '../styles/auth.css';

/** P7: v1's home page diagnostics (account, role, live access checks, token claims), now under Account → Session. */
export function SessionPage() {
  const { user } = useAuth();
  if (!user) return null;
  return (
    <div className="stack">
      <div className="page-header">
        <h1>Account</h1>
        <p>Your AuthService account, what your role allows, and the access token the portal is using.</p>
      </div>
      <AccountTabs />
      <div className="grid">
        <ProfileCard user={user} />
        <RoleCapabilitiesCard role={user.role} />
      </div>
      <AccessCheckCard user={user} />
      <TokenCard />
    </div>
  );
}
