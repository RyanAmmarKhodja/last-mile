import { useAuth } from '@/shared/context/AuthContext';
import { ChangeEmailForm } from '../components/ChangeEmailForm';
import { ChangePasswordForm } from '../components/ChangePasswordForm';
import { DeleteAccountCard } from '../components/DeleteAccountCard';
import '../styles/auth.css';

export function AccountSettingsPage() {
  const { user, hasRole } = useAuth();
  if (!user) return null;

  return (
    <div className="stack settings">
      <div className="page-header">
        <h1>Account settings</h1>
        <p>
          {hasRole('ADMIN')
            ? 'Change your own email or password here. Roles and account status are managed on the Users page.'
            : 'Change your email or password. Only an administrator can change your role or disable your account.'}
        </p>
      </div>
      <div className="grid">
        <ChangeEmailForm key={user.email} user={user} />
        <ChangePasswordForm user={user} />
      </div>
      <DeleteAccountCard user={user} />
    </div>
  );
}
