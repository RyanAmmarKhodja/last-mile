import { useState, type FormEvent } from 'react';
import { getErrorMessage, usersApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Button, Card, TextField } from '@/shared/components';
import type { User } from '@/shared/types/auth';

export function DeleteAccountCard({ user }: { user: User }) {
  const { logout } = useAuth();
  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await usersApi.remove(user.id);
      await logout(`Account ${user.email} was deleted.`);
    } catch (err) {
      setError(getErrorMessage(err));
      setSubmitting(false);
    }
  }

  return (
    <Card title="Delete account" subtitle="Permanently removes your account and signs you out." className="danger">
      <form className="form" onSubmit={handleSubmit} noValidate>
        {error && <Alert variant="error">{error}</Alert>}
        <TextField label={`Type ${user.email} to confirm`} value={confirmation} autoComplete="off"
          onChange={(e) => setConfirmation(e.target.value)} />
        <div>
          <Button type="submit" variant="danger" loading={submitting} disabled={confirmation.trim().toLowerCase() !== user.email}>
            Delete my account
          </Button>
        </div>
      </form>
    </Card>
  );
}
