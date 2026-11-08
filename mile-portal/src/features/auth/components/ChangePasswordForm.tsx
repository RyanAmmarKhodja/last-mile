import { useState, type FormEvent } from 'react';
import { getErrorMessage, usersApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Button, Card, TextField } from '@/shared/components';
import type { User } from '@/shared/types/auth';
import { PASSWORD_HINT, validateConfirmation, validatePassword } from '../utils/validation';

/** Changing the password revokes every refresh token server-side, so we sign out right after. */
export function ChangePasswordForm({ user }: { user: User }) {
  const { logout } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const validation =
      (!currentPassword && 'Enter your current password.') ||
      validatePassword(password) ||
      validateConfirmation(password, confirm) ||
      (password === currentPassword && 'The new password must differ from the current one.');
    if (validation) return setError(validation);

    setError(null);
    setSubmitting(true);
    try {
      await usersApi.update(user.id, { password, currentPassword });
      await logout('Password changed. All sessions were signed out. Sign in with your new password.');
    } catch (err) {
      setError(getErrorMessage(err));
      setSubmitting(false);
    }
  }

  return (
    <Card title="Password" subtitle="You will be signed out of every session after changing it.">
      <form className="form" onSubmit={handleSubmit} noValidate>
        {error && <Alert variant="error">{error}</Alert>}
        <TextField label="Current password" type="password" autoComplete="current-password" value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)} />
        <TextField label="New password" type="password" autoComplete="new-password" hint={PASSWORD_HINT} value={password}
          onChange={(e) => setPassword(e.target.value)} />
        <TextField label="Confirm new password" type="password" autoComplete="new-password" value={confirm}
          onChange={(e) => setConfirm(e.target.value)} />
        <div>
          <Button type="submit" loading={submitting}>
            Change password
          </Button>
        </div>
      </form>
    </Card>
  );
}
