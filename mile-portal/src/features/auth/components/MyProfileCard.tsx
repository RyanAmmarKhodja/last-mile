import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fleetApi, getErrorMessage, getErrorStatus, profilesApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { Alert, Button, Card, QueryState, TextField } from '@/shared/components';
import type { User } from '@/shared/types/auth';
import type { AdminProfile, ClientProfile } from '@/shared/types/profiles';
import type { Driver } from '@/shared/types/fleet';
import { DriverForm } from '@/features/fleet';

/**
 * FR-9: the profile lives with its owner (D2): ProfileService for CLIENT / ADMIN, FleetService for DRIVER.
 * PUT …/me creates the profile when it is missing, so a 404 just shows an empty form.
 */
export function MyProfileCard({ user }: { user: User }) {
  if (user.role === 'DRIVER') return <DriverProfile />;
  if (user.role === 'ADMIN') return <AdminProfileForm />;
  return <ClientProfileForm />;
}

function useSaved() {
  const [saved, setSaved] = useState(false);
  return { saved, flash: () => (setSaved(true), setTimeout(() => setSaved(false), 3000)) };
}

function ClientProfileForm() {
  const client = useQueryClient();
  const q = useQuery({ queryKey: qk.myClientProfile, queryFn: profilesApi.myClient, retry: false });
  const missing = getErrorStatus(q.error) === 404;
  const { saved, flash } = useSaved();
  const save = useMutation({
    mutationFn: profilesApi.saveMyClient,
    onSuccess: (p) => (client.setQueryData(qk.myClientProfile, p), flash()),
  });
  return (
    <Card title="Customer profile" subtitle="Your name, phone and default pick-up / delivery address (ProfileService).">
      <QueryState isLoading={q.isLoading} error={missing ? null : q.error}>
        {missing && <Alert variant="info">Your profile has not been created yet. Fill it in to send and receive parcels.</Alert>}
        <ClientFields key={q.data?.updatedAt ?? 'new'} initial={q.data} pending={save.isPending} error={save.error} saved={saved} onSubmit={(b) => save.mutate(b)} />
      </QueryState>
    </Card>
  );
}

function ClientFields({ initial, pending, error, saved, onSubmit }: { initial?: ClientProfile; pending: boolean; error: unknown; saved: boolean; onSubmit: (b: { firstName: string; lastName: string; companyName: string | null; phoneNumber: string; address: string }) => void }) {
  const [f, setF] = useState({
    firstName: initial?.firstName ?? '',
    lastName: initial?.lastName ?? '',
    companyName: initial?.companyName ?? '',
    phoneNumber: initial?.phoneNumber ?? '',
    address: initial?.address ?? '',
  });
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF({ ...f, [k]: e.target.value });
  function submit(e: FormEvent) {
    e.preventDefault();
    onSubmit({ firstName: f.firstName.trim(), lastName: f.lastName.trim(), companyName: f.companyName.trim() || null, phoneNumber: f.phoneNumber.trim(), address: f.address.trim() });
  }
  return (
    <form className="form" onSubmit={submit}>
      <div className="form-grid">
        <TextField label="First name" required maxLength={100} value={f.firstName} onChange={set('firstName')} />
        <TextField label="Last name" required maxLength={100} value={f.lastName} onChange={set('lastName')} />
        <TextField label="Phone number" required maxLength={50} value={f.phoneNumber} onChange={set('phoneNumber')} />
        <TextField label="Company (optional)" maxLength={255} value={f.companyName} onChange={set('companyName')} />
      </div>
      <div className="field">
        <label htmlFor="profile-address">Address</label>
        <textarea id="profile-address" required maxLength={1000} value={f.address} onChange={set('address')} />
        <span className="field-hint">Used as the default origin of what you send and the destination of what you receive. Existing shipments keep their address.</span>
      </div>
      {error ? <Alert variant="error">{getErrorMessage(error)}</Alert> : null}
      {saved && <Alert variant="success">Profile saved.</Alert>}
      <div className="row">
        <Button type="submit" loading={pending}>
          Save profile
        </Button>
      </div>
    </form>
  );
}

function AdminProfileForm() {
  const client = useQueryClient();
  const q = useQuery({ queryKey: qk.myAdminProfile, queryFn: profilesApi.myAdmin, retry: false });
  const missing = getErrorStatus(q.error) === 404;
  const { saved, flash } = useSaved();
  const save = useMutation({
    mutationFn: profilesApi.saveMyAdmin,
    onSuccess: (p) => (client.setQueryData(qk.myAdminProfile, p), flash()),
  });
  return (
    <Card title="Administrator profile" subtitle="ProfileService">
      <QueryState isLoading={q.isLoading} error={missing ? null : q.error}>
        <AdminFields key={q.data?.updatedAt ?? 'new'} initial={q.data} pending={save.isPending} error={save.error} saved={saved} onSubmit={(b) => save.mutate(b)} />
      </QueryState>
    </Card>
  );
}

function AdminFields({ initial, pending, error, saved, onSubmit }: { initial?: AdminProfile; pending: boolean; error: unknown; saved: boolean; onSubmit: (b: { firstName: string; lastName: string; phoneNumber: string | null }) => void }) {
  const [first, setFirst] = useState(initial?.firstName ?? '');
  const [last, setLast] = useState(initial?.lastName ?? '');
  const [phone, setPhone] = useState(initial?.phoneNumber ?? '');
  return (
    <form className="form" onSubmit={(e) => (e.preventDefault(), onSubmit({ firstName: first.trim(), lastName: last.trim(), phoneNumber: phone.trim() || null }))}>
      <div className="form-grid">
        <TextField label="First name" required maxLength={100} value={first} onChange={(e) => setFirst(e.target.value)} />
        <TextField label="Last name" required maxLength={100} value={last} onChange={(e) => setLast(e.target.value)} />
        <TextField label="Phone (optional)" maxLength={50} value={phone} onChange={(e) => setPhone(e.target.value)} />
      </div>
      {error ? <Alert variant="error">{getErrorMessage(error)}</Alert> : null}
      {saved && <Alert variant="success">Profile saved.</Alert>}
      <div className="row">
        <Button type="submit" loading={pending}>
          Save profile
        </Button>
      </div>
    </form>
  );
}

function DriverProfile() {
  const client = useQueryClient();
  const q = useQuery({ queryKey: qk.myDriver, queryFn: fleetApi.me, retry: false });
  const missing = getErrorStatus(q.error) === 404;
  const { saved, flash } = useSaved();
  const save = useMutation({
    mutationFn: fleetApi.saveMe,
    onSuccess: (d: Driver) => (client.setQueryData(qk.myDriver, d), flash()),
  });
  return (
    <Card title="Driver profile" subtitle="FleetService. Your licence number must be unique.">
      <QueryState isLoading={q.isLoading} error={missing ? null : q.error}>
        {missing && <Alert variant="info">Your driver profile is missing (for example, the licence number was already taken at registration). Fill it in here.</Alert>}
        <DriverForm
          key={q.data?.updatedAt ?? 'new'}
          initial={{ firstName: q.data?.firstName ?? '', lastName: q.data?.lastName ?? '', phoneNumber: q.data?.phoneNumber ?? '', licenseNumber: q.data?.licenseNumber ?? '' }}
          submitLabel="Save profile"
          pending={save.isPending}
          error={save.error ? getErrorMessage(save.error) : null}
          onSubmit={(b) => save.mutate(b)}
        />
        {saved && <Alert variant="success">Profile saved.</Alert>}
      </QueryState>
    </Card>
  );
}
