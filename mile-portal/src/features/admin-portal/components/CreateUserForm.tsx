import { useState, type FormEvent } from 'react';
import { getErrorMessage, usersApi } from '@/shared/api';
import { Alert, Button, SelectField, TextField } from '@/shared/components';
import { USER_ROLES, type User, type UserRole } from '@/shared/types/auth';
import { PASSWORD_HINT, validateEmail, validatePassword } from '@/features/auth/utils/validation';

const ROLE_OPTIONS = USER_ROLES.map((r) => ({ value: r, label: r }));

/** POST /users: admins can create any human role, including ADMIN. */
export function CreateUserForm({ onCreated }: { onCreated: (user: User) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<UserRole>('DRIVER');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const validation = validateEmail(email) || validatePassword(password);
    if (validation) return setError(validation);

    setError(null);
    setSubmitting(true);
    try {
      const user = await usersApi.create({ email: email.trim(), password, role });
      setEmail('');
      setPassword('');
      onCreated(user);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="form create-user" onSubmit={handleSubmit} noValidate>
      {error && <Alert variant="error">{error}</Alert>}
      <div className="create-user-fields">
        <TextField label="Email" type="email" autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} />
        <TextField label="Password" type="password" autoComplete="new-password" hint={PASSWORD_HINT} value={password}
          onChange={(e) => setPassword(e.target.value)} />
        <SelectField label="Role" options={ROLE_OPTIONS} value={role} onChange={(e) => setRole(e.target.value as UserRole)} />
      </div>
      <div>
        <Button type="submit" loading={submitting}>
          Create user
        </Button>
      </div>
    </form>
  );
}
