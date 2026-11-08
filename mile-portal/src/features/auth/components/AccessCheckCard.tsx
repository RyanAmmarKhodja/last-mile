import { useState } from 'react';
import { api, getErrorMessage, getErrorStatus } from '@/shared/api';
import { Button, Card } from '@/shared/components';
import type { User, UserRole } from '@/shared/types/auth';

interface Probe {
  id: string;
  label: string;
  method: 'GET';
  path: string;
  /** Roles the backend should allow; undefined = any signed-in user. */
  allowed?: UserRole[];
  /** Builds the request path, e.g. to insert the current user id. */
  url: (user: User) => string;
}

const PROBES: Probe[] = [
  { id: 'me', label: 'Own profile', method: 'GET', path: '/users/me', url: () => '/users/me' },
  { id: 'self', label: 'Own account by id', method: 'GET', path: '/users/{myId}', url: (u) => `/users/${u.id}` },
  {
    id: 'other',
    label: "Someone else's account",
    method: 'GET',
    path: '/users/{otherId}',
    allowed: ['ADMIN'],
    url: () => '/users/00000000-0000-7000-8000-000000000000',
  },
  { id: 'list', label: 'List all users', method: 'GET', path: '/users', allowed: ['ADMIN'], url: () => '/users?pageSize=1' },
  { id: 'validate', label: 'Gateway token check', method: 'GET', path: '/auth/validate', url: () => '/auth/validate' },
];

interface Result {
  status: number | 'ERR';
  message: string;
}

/**
 * Calls real endpoints with the current token so you can see the backend (not just the UI)
 * enforcing roles. Admin probing an unknown id gets 404 (allowed, but not found).
 */
export function AccessCheckCard({ user }: { user: User }) {
  const [results, setResults] = useState<Record<string, Result>>({});
  const [running, setRunning] = useState(false);

  async function runProbe(probe: Probe): Promise<[string, Result]> {
    try {
      const res = await api.get(probe.url(user));
      const summary =
        probe.id === 'list' ? `${(res.data as { totalCount: number }).totalCount} users visible` : 'OK';
      return [probe.id, { status: res.status, message: summary }];
    } catch (err) {
      return [probe.id, { status: getErrorStatus(err) ?? 'ERR', message: getErrorMessage(err) }];
    }
  }

  async function runAll() {
    setRunning(true);
    const entries = await Promise.all(PROBES.map(runProbe));
    setResults(Object.fromEntries(entries));
    setRunning(false);
  }

  return (
    <Card
      title="Server-side access check"
      subtitle="Calls AuthService through the gateway with your current access token."
      actions={
        <Button size="sm" onClick={runAll} loading={running}>
          Run checks
        </Button>
      }
    >
      <div className="table-wrap">
        <table className="table probes">
          <thead>
            <tr>
              <th>Endpoint</th>
              <th>Who may call it</th>
              <th>Expected for you</th>
              <th>Result</th>
            </tr>
          </thead>
          <tbody>
            {PROBES.map((p) => {
              const expectedAllowed = !p.allowed || p.allowed.includes(user.role);
              const result = results[p.id];
              const gotAllowed = result && typeof result.status === 'number' && result.status !== 401 && result.status !== 403;
              const matches = result && gotAllowed === expectedAllowed;
              return (
                <tr key={p.id}>
                  <td>
                    <div>{p.label}</div>
                    <code className="muted">
                      {p.method} {p.path}
                    </code>
                  </td>
                  <td>{p.allowed ? p.allowed.join(', ') : 'Any signed-in user'}</td>
                  <td>{expectedAllowed ? 'Allowed' : 'Denied (403)'}</td>
                  <td>
                    {result ? (
                      <span className={`probe-result ${matches ? 'match' : 'mismatch'}`} title={result.message}>
                        <strong>{result.status}</strong> {result.message}
                      </span>
                    ) : (
                      <span className="muted">—</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Card>
  );
}
