import type { ChangeEvent } from 'react';
import { useState, type FormEvent } from 'react';
import { Alert, Button, TextField } from '@/shared/components';
import type { DriverInput } from '@/shared/types/fleet';

interface Props {
  initial: DriverInput;
  submitLabel: string;
  pending: boolean;
  error: string | null;
  onSubmit: (body: DriverInput) => void;
  onCancel?: () => void;
}

/** FleetService driver profile (full replacement): used by the admin edit dialog and the driver's own profile. */
export function DriverForm({ initial, submitLabel, pending, error, onSubmit, onCancel }: Props) {
  const [form, setForm] = useState<DriverInput>(initial);
  const set = (k: keyof DriverInput) => (e: ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value });
  function submit(e: FormEvent) {
    e.preventDefault();
    onSubmit({
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      phoneNumber: form.phoneNumber.trim(),
      licenseNumber: form.licenseNumber.trim(),
    });
  }
  return (
    <form className="form" onSubmit={submit}>
      <div className="form-grid">
        <TextField label="First name" required maxLength={100} value={form.firstName} onChange={set('firstName')} />
        <TextField label="Last name" required maxLength={100} value={form.lastName} onChange={set('lastName')} />
        <TextField label="Phone number" required maxLength={50} value={form.phoneNumber} onChange={set('phoneNumber')} />
        <TextField label="Driving licence number" required maxLength={100} value={form.licenseNumber} onChange={set('licenseNumber')} />
      </div>
      {error && <Alert variant="error">{error}</Alert>}
      <div className="dialog-actions">
        {onCancel && (
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" loading={pending}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
