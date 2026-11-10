import { TextField } from '@/shared/components';
import type { RegistrationProfile, UserRole } from '@/shared/types/auth';
import '../styles/auth.css';
import { PROFILE_FIELDS, type ProfileErrors, type ProfileForm } from '../utils/validation';

const LABELS: Record<keyof RegistrationProfile, string> = {
  firstName: 'First name',
  lastName: 'Last name',
  phoneNumber: 'Phone number',
  companyName: 'Company',
  address: 'Address',
  licenseNumber: 'Driving license number',
};

const AUTOCOMPLETE: Partial<Record<keyof RegistrationProfile, string>> = {
  firstName: 'given-name',
  lastName: 'family-name',
  phoneNumber: 'tel',
  companyName: 'organization',
  address: 'street-address',
};

interface Props {
  role: UserRole;
  value: ProfileForm;
  errors?: ProfileErrors;
  onChange: (next: ProfileForm) => void;
  /** Turn off browser autofill (admin creating someone else's account). */
  noAutofill?: boolean;
}

/**
 * Profile fields for the selected role. AuthService validates them and relays them in UserRegistered.
 * ProfileService (clients, admins) or FleetService (drivers) stores them.
 */
export function ProfileFields({ role, value, errors = {}, onChange, noAutofill }: Props) {
  return (
    <div className="profile-fields">
      {PROFILE_FIELDS[role].map(({ field, required }) => (
        <TextField
          key={field}
          label={required ? LABELS[field] : `${LABELS[field]} (optional)`}
          type={field === 'phoneNumber' ? 'tel' : 'text'}
          autoComplete={noAutofill ? 'off' : AUTOCOMPLETE[field] ?? 'off'}
          value={value[field]}
          error={errors[field]}
          onChange={(e) => onChange({ ...value, [field]: e.target.value })}
        />
      ))}
    </div>
  );
}
