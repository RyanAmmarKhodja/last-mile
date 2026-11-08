import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { getErrorMessage, usersApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Button, Card, Spinner } from '@/shared/components';
import { USER_ROLES, type PagedResult, type User, type UserRole } from '@/shared/types/auth';
import { validatePassword } from '@/features/auth/utils/validation';
import { CreateUserForm } from '../components/CreateUserForm';
import { UserTable } from '../components/UserTable';
import '../styles/admin.css';

const PAGE_SIZE = 10;

type Flash = { variant: 'success' | 'error'; text: string } | null;

export function UsersPage() {
  const { user: me, refreshUser, logout } = useAuth();
  const [page, setPage] = useState(1);
  const [role, setRole] = useState<UserRole | ''>('');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [data, setData] = useState<PagedResult<User> | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [flash, setFlash] = useState<Flash>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setData(await usersApi.list({ page, pageSize: PAGE_SIZE, role: role || undefined, search: search || undefined }));
    } catch (err) {
      setLoadError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [page, role, search]);

  useEffect(() => {
    void load();
  }, [load]);

  function applySearch(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  /** Runs an update and patches the row in place. Self-edits refresh the signed-in profile. */
  async function mutate(target: User, action: () => Promise<User | void>, successText: string) {
    setBusyId(target.id);
    setFlash(null);
    try {
      const updated = await action();
      if (updated) setData((d) => d && { ...d, items: d.items.map((u) => (u.id === updated.id ? updated : u)) });
      setFlash({ variant: 'success', text: successText });
      if (target.id === me?.id) await refreshUser();
    } catch (err) {
      setFlash({ variant: 'error', text: getErrorMessage(err) });
    } finally {
      setBusyId(null);
    }
  }

  function handleChangeRole(target: User, newRole: UserRole) {
    if (newRole === target.role) return;
    const selfWarning =
      target.id === me?.id ? '\n\nThis is your own account: you will lose admin access once your token is replaced.' : '';
    if (!window.confirm(`Change ${target.email} from ${target.role} to ${newRole}?${selfWarning}`)) return;
    void mutate(target, () => usersApi.update(target.id, { role: newRole }), `${target.email} is now ${newRole}. Their sessions were revoked.`);
  }

  function handleToggleActive(target: User) {
    const next = !target.isActive;
    if (!window.confirm(`${next ? 'Enable' : 'Disable'} ${target.email}?`)) return;
    void mutate(target, () => usersApi.update(target.id, { isActive: next }),
      next ? `${target.email} can sign in again.` : `${target.email} is disabled and was signed out of all sessions.`);
  }

  function handleResetPassword(target: User) {
    const password = window.prompt(`New password for ${target.email} (8+ chars, a letter and a digit):`);
    if (password === null) return;
    const invalid = validatePassword(password);
    if (invalid) return setFlash({ variant: 'error', text: invalid });
    void mutate(target, () => usersApi.update(target.id, { password }), `Password reset for ${target.email}. Their sessions were revoked.`);
  }

  async function handleDelete(target: User) {
    if (!window.confirm(`Permanently delete ${target.email}?`)) return;
    setBusyId(target.id);
    setFlash(null);
    try {
      await usersApi.remove(target.id);
      if (target.id === me?.id) return void (await logout('You deleted your own account.'));
      setFlash({ variant: 'success', text: `${target.email} was deleted.` });
      await load();
    } catch (err) {
      setFlash({ variant: 'error', text: getErrorMessage(err) });
    } finally {
      setBusyId(null);
    }
  }

  function handleCreated(created: User) {
    setFlash({ variant: 'success', text: `Created ${created.email} (${created.role}).` });
    setShowCreate(false);
    void load();
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Users</h1>
        <p>ADMIN only. Every action calls AuthService's /api/v1/users endpoints.</p>
      </div>

      {flash && (
        <Alert variant={flash.variant} onClose={() => setFlash(null)}>
          {flash.text}
        </Alert>
      )}

      <Card
        title="Create user"
        subtitle="Unlike self-registration, admins can create ADMIN accounts."
        actions={
          <Button size="sm" variant="secondary" onClick={() => setShowCreate((s) => !s)}>
            {showCreate ? 'Close' : 'New user'}
          </Button>
        }
      >
        {showCreate ? <CreateUserForm onCreated={handleCreated} /> : <p className="muted">Open the form to add an account.</p>}
      </Card>

      <Card
        title="All accounts"
        subtitle={data ? `${data.totalCount} total` : undefined}
        actions={
          <Button size="sm" variant="ghost" onClick={() => void load()} disabled={loading}>
            Reload
          </Button>
        }
      >
        <form className="row filters" onSubmit={applySearch}>
          <input className="input" type="search" placeholder="Search by email" value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)} />
          <select className="input" value={role} aria-label="Filter by role"
            onChange={(e) => {
              setPage(1);
              setRole(e.target.value as UserRole | '');
            }}>
            <option value="">All roles</option>
            {USER_ROLES.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
          <Button type="submit" size="sm">
            Search
          </Button>
        </form>

        {loadError && <Alert variant="error">{loadError}</Alert>}
        {loading && !data ? (
          <div className="center-block">
            <Spinner />
          </div>
        ) : (
          data && (
            <>
              <UserTable users={data.items} currentUserId={me?.id ?? ''} busyId={busyId} onChangeRole={handleChangeRole}
                onToggleActive={handleToggleActive} onResetPassword={handleResetPassword} onDelete={(u) => void handleDelete(u)} />
              <div className="pagination">
                <Button size="sm" variant="ghost" disabled={page <= 1 || loading} onClick={() => setPage((p) => p - 1)}>
                  Previous
                </Button>
                <span className="muted">
                  Page {page} of {totalPages}
                </span>
                <Button size="sm" variant="ghost" disabled={page >= totalPages || loading} onClick={() => setPage((p) => p + 1)}>
                  Next
                </Button>
              </div>
            </>
          )
        )}
      </Card>
    </div>
  );
}
