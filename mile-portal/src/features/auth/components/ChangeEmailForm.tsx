import { useState, type FormEvent } from 'react';
import { getErrorMessage, usersApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Button, Card, TextField } from '@/shared/components';
import type { User } from '@/shared/types/auth';
import { validateEmail } from '../utils/validation';

export function ChangeEmailForm({ user }: { user: User }) {
  const { refreshUser } = useAuth();
  const [email, setEmail] = useState(user.email);
  const [currentPassword, setCurrentPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSuccess(null);
    const emailError = validateEmail(email);
    if (emailError) return setError(emailError);
    if (email.trim().toLowerCase() === user.email) return setError('That is already your email.');
    if (!currentPassword) return setError('Enter your current password to confirm.');

    setError(null);
    setSubmitting(true);
    try {
      await usersApi.update(user.id, { email: email.trim(), currentPassword });
      await refreshUser();
      setCurrentPassword('');
      setSuccess('Email updated. Use the new address next time you sign in.');
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card title="Email" subtitle="Your email is also your login.">
      <form className="form" onSubmit={handleSubmit} noValidate>
        {error && <Alert variant="error">{error}</Alert>}
        {success && <Alert variant="success">{success}</Alert>}
        <TextField label="New email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <TextField label="Current password" type="password" autoComplete="current-password" value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)} />
        <div>
          <Button type="submit" loading={submitting}>
            Update email
          </Button>
        </div>
      </form>
    </Card>
  );
}
