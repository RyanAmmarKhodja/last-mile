import { useState, type FormEvent } from 'react';
import { useAuth } from '@/shared/context/AuthContext';
import { getErrorMessage } from '@/shared/api';
import { Alert, Button, TextField } from '@/shared/components';
import { validateEmail } from '../utils/validation';

export function LoginForm({ initialEmail = '', onSuccess }: { initialEmail?: string; onSuccess?: () => void }) {
  const { login } = useAuth();
  const [email, setEmail] = useState(initialEmail);
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const emailError = validateEmail(email);
    if (emailError || !password) {
      setError(emailError ?? 'Password is required.');
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await login(email.trim(), password);
      onSuccess?.();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {error && <Alert variant="error">{error}</Alert>}
      <TextField label="Email" type="email" autoComplete="email" autoFocus={!initialEmail} value={email}
        onChange={(e) => setEmail(e.target.value)} />
      <TextField label="Password" type="password" autoComplete="current-password" autoFocus={!!initialEmail}
        value={password} onChange={(e) => setPassword(e.target.value)} />
      <Button type="submit" className="btn-block" loading={submitting}>
        Sign in
      </Button>
    </form>
  );
}
