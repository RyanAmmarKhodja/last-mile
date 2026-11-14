import { useAuth } from '@/shared/context/AuthContext';
import { ChangeEmailForm } from '../components/ChangeEmailForm';
import { ChangePasswordForm } from '../components/ChangePasswordForm';
import { DeleteAccountCard } from '../components/DeleteAccountCard';
import { AccountTabs } from '../components/AccountTabs';
import { MyProfileCard } from '../components/MyProfileCard';
import '../styles/auth.css';

/** /account: the role's profile (ProfileService / FleetService). */
export function AccountProfilePage() {
  const { user } = useAuth();
  if (!user) return null;
  return (
    <div className="stack settings">
      <div className="page-header">
        <h1>Account</h1>
        <p>Your profile is shared with the people and drivers involved in your shipments.</p>
      </div>
      <AccountTabs />
      <MyProfileCard user={user} />
    </div>
  );
}

/** /account/security: AuthService credentials (v1). */
export function AccountSettingsPage() {
  const { user, hasRole } = useAuth();
  if (!user) return null;

  return (
    <div className="stack settings">
      <div className="page-header">
        <h1>Account</h1>
        <p>
          {hasRole('ADMIN')
            ? 'Change your own email or password here. Roles and account status are managed on the Users page.'
            : 'Change your email or password. Only an administrator can change your role or disable your account.'}
        </p>
      </div>
      <AccountTabs />
      <div className="grid">
        <ChangeEmailForm key={user.email} user={user} />
        <ChangePasswordForm user={user} />
      </div>
      <DeleteAccountCard user={user} />
    </div>
  );
}
