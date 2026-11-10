import { useState, type FormEvent } from 'react';
import { authApi, getErrorMessage } from '@/shared/api';
import { Alert, Button, TextField } from '@/shared/components';
import { SELF_REGISTER_ROLES, type User, type UserRole } from '@/shared/types/auth';
import {
  EMPTY_PROFILE,
  PASSWORD_HINT,
  toProfilePayload,
  validateConfirmation,
  validateEmail,
  validatePassword,
  validateProfile,
  type ProfileErrors,
  type ProfileForm,
} from '../utils/validation';
import { ROLE_CAPABILITIES } from '../utils/roleCapabilities';
import { ProfileFields } from './ProfileFields';

type Errors = Partial<Record<'email' | 'password' | 'confirm', string>>;

export function RegisterForm({ onRegistered }: { onRegistered: (user: User) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [role, setRole] = useState<UserRole>('CLIENT');
  const [profile, setProfile] = useState<ProfileForm>(EMPTY_PROFILE);
  const [errors, setErrors] = useState<Errors>({});
  const [profileErrors, setProfileErrors] = useState<ProfileErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const next: Errors = {
      email: validateEmail(email) ?? undefined,
      password: validatePassword(password) ?? undefined,
      confirm: validateConfirmation(password, confirm) ?? undefined,
    };
    const nextProfile = validateProfile(role, profile);
    setErrors(next);
    setProfileErrors(nextProfile);
    if (Object.values(next).some(Boolean) || Object.keys(nextProfile).length > 0) return;

    setServerError(null);
    setSubmitting(true);
    try {
      onRegistered(
        await authApi.register({ email: email.trim(), password, role, profile: toProfilePayload(role, profile) }),
      );
    } catch (err) {
      setServerError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      {serverError && <Alert variant="error">{serverError}</Alert>}
      <TextField label="Email" type="email" autoComplete="email" autoFocus value={email} error={errors.email}
        onChange={(e) => setEmail(e.target.value)} />
      <TextField label="Password" type="password" autoComplete="new-password" value={password} hint={PASSWORD_HINT}
        error={errors.password} onChange={(e) => setPassword(e.target.value)} />
      <TextField label="Confirm password" type="password" autoComplete="new-password" value={confirm}
        error={errors.confirm} onChange={(e) => setConfirm(e.target.value)} />

      <fieldset className="role-picker">
        <legend>Account type</legend>
        {SELF_REGISTER_ROLES.map((r) => (
          <label key={r} className={`role-option ${role === r ? 'selected' : ''}`}>
            <input type="radio" name="role" value={r} checked={role === r} onChange={() => { setRole(r); setProfileErrors({}); }} />
            <span className="role-option-name">{r}</span>
            <span className="role-option-desc">{ROLE_CAPABILITIES[r].summary}</span>
          </label>
        ))}
        <span className="field-hint">ADMIN accounts can only be created by an administrator.</span>
      </fieldset>

      <ProfileFields role={role} value={profile} errors={profileErrors} onChange={setProfile} />

      <Button type="submit" className="btn-block" loading={submitting}>
        Create account
      </Button>
    </form>
  );
}
